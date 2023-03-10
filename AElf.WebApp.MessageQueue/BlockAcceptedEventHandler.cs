using System;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Events;
using AElf.WebApp.MessageQueue.Enum;
using AElf.WebApp.MessageQueue.Provider;
using AElf.WebApp.MessageQueue.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AElf.WebApp.MessageQueue;

public class BlockAcceptedEventHandler : ILocalEventHandler<BlockAcceptedEvent>, ITransientDependency
{
    private readonly IBlockMessageService _blockMessageService;
    private readonly ISyncBlockStateProvider _syncBlockStateProvider;
    private readonly ISendMessageByDesignateHeightTaskManager _sendMessageByDesignateHeightTaskManager;
    private readonly ISyncBlockLatestHeightProvider _latestHeightProvider;
    private readonly ILogger<BlockAcceptedEventHandler> _logger;

    public BlockAcceptedEventHandler(
        ISyncBlockStateProvider syncBlockStateProvider,
        ISendMessageByDesignateHeightTaskManager sendMessageByDesignateHeightTaskManager,
        IBlockMessageService blockMessageService, ILogger<BlockAcceptedEventHandler> logger,
        ISyncBlockLatestHeightProvider latestHeightProvider)
    {
        _syncBlockStateProvider = syncBlockStateProvider;
        _sendMessageByDesignateHeightTaskManager = sendMessageByDesignateHeightTaskManager;
        _blockMessageService = blockMessageService;
        _logger = logger;
        _latestHeightProvider = latestHeightProvider;
    }

    public async Task HandleEventAsync(BlockAcceptedEvent eventData)
    {
        var blockSyncState = await _syncBlockStateProvider.GetCurrentStateAsync();
        switch (blockSyncState.State)
        {
            case SyncState.Stopped:
            case SyncState.AsyncRunning:
                break;
            case SyncState.Stopping:
                await StopAsync();
                break;
            case SyncState.SyncPrepared:
                await AsyncPreparedToRun(eventData);
                break;
            case SyncState.SyncRunning:
                await RunningAsync(eventData);
                break;
            case SyncState.Prepared:
                await PreparedToRunAsync(eventData);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _latestHeightProvider.SetLatestHeight(eventData.Block.Height);
        }

    private async Task StopAsync()
    {
        _logger.LogInformation("Publish message is stopping");
        await _sendMessageByDesignateHeightTaskManager.StopAsync();
        await _syncBlockStateProvider.UpdateStateAsync(null, SyncState.Stopped);
        _logger.LogInformation("Publish message has stopped");
    }

    private async Task RunningAsync(BlockAcceptedEvent eventData)
    {
        _logger.LogInformation("Publish message synchronously");
        await _blockMessageService.SendMessageAsync(eventData.BlockExecutedSet);
    }

    private async Task PreparedToRunAsync(BlockAcceptedEvent eventData)
    {
        await _sendMessageByDesignateHeightTaskManager.StopAsync();
        var blockSyncState = await _syncBlockStateProvider.GetCurrentStateAsync();
        if (blockSyncState.CurrentHeight >= eventData.Block.Height)
        {
            _logger.LogInformation("Publish message synchronously");
            await _blockMessageService.SendMessageAsync(eventData.BlockExecutedSet);
            await _syncBlockStateProvider.UpdateStateAsync(null, SyncState.SyncRunning);
        }

        else if (blockSyncState.CurrentHeight < eventData.Block.Height - 1)
        {
            await _syncBlockStateProvider.UpdateStateAsync(null, SyncState.AsyncRunning);
            _logger.LogInformation("Start to publish message asynchronously");
            await _sendMessageByDesignateHeightTaskManager.StartAsync();
        }
       
    }

    private  async Task AsyncPreparedToRun(BlockAcceptedEvent eventData)
    {
        await _sendMessageByDesignateHeightTaskManager.StopAsync();
        var currentHeight = eventData.Block.Height;
        var blockSyncState = await _syncBlockStateProvider.GetCurrentStateAsync();
        if (blockSyncState.CurrentHeight+1 >= currentHeight)
        {
            return;
        }

        var from = blockSyncState.CurrentHeight+1;
        var to = currentHeight - 1;
        if (from > to + 1 || to - from > 10)
        {
            await _syncBlockStateProvider.UpdateStateAsync(null, SyncState.Prepared);
            return;
        }

        await _syncBlockStateProvider.UpdateStateAsync(null, SyncState.SyncRunning);
        if (from <= to)
        {
            _logger.LogInformation($"Catch up to current block, from: {from } - to: {to }");
        }

        for (var i = from; i <= to; i++)
        {
            await _blockMessageService.SendMessageAsync(i);
        }

        await _blockMessageService.SendMessageAsync(eventData.BlockExecutedSet);
    }
}