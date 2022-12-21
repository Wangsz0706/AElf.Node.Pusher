using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.WebApp.Application.MessageQueue.Tests.Helps;
using AElf.WebApp.MessageQueue;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace AElf.WebApp.Application.MessageQueue.Tests;


[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(MessageQueueAElfModule),
    typeof(KernelCoreTestAElfModule),
    typeof(AbpEventBusModule)
)]
public class WebAppMessageQueueTestAElfModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        var services = context.Services;
        services.AddSingleton<MockChainHelper>();
        /*services.AddSingleton<IBlockchainService>();*/
        services.AddDistributedMemoryCache();
    }
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var mockChainHelper = context.ServiceProvider.GetService<MockChainHelper>();
        var otherChain = AsyncHelper.RunSync(() => mockChainHelper.MockOtherChainAsync());
        
    }
}