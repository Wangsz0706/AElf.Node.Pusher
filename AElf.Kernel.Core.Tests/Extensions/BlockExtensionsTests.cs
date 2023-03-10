using System.Collections.Generic;
using AElf.Cryptography;
using AElf.Kernel.TestBase;

namespace AElf.Kernel;

[Trait("Category", AElfBlockchainModule)]
public sealed class BlockExtensionsTests : AElfKernelTestBase
{
    private readonly KernelTestHelper _kernelTestHelper;

    public BlockExtensionsTests()
    {
        _kernelTestHelper = GetRequiredService<KernelTestHelper>();
    }

    [Fact]
    public void VerifySignature_Test()
    {
        {
            var block = GenerateBlock();
            block.Header.Height = -1;
            block.VerifySignature().ShouldBeFalse();
        }

        {
            var block = GenerateBlock();
            block.Body.TransactionIds.Clear();
            block.VerifySignature().ShouldBeFalse();
        }

        {
            var block = GenerateBlock();
            block.Header.Signature = ByteString.Empty;
            block.VerifySignature().ShouldBeFalse();
        }

        {
            var block = GenerateBlock();
            block.Header.Signature = ByteString.CopyFromUtf8("Signature");
            block.VerifySignature().ShouldBeFalse();
        }

        {
            var block = GenerateBlock();
            block.Header.SignerPubkey = ByteString.CopyFromUtf8("SignerPubkey");
            block.VerifySignature().ShouldBeFalse();
        }

        {
            var block = GenerateBlock();
            block.VerifySignature().ShouldBeTrue();
        }
    }

    private Block GenerateBlock()
    {
        var transaction = _kernelTestHelper.GenerateTransaction();
        var block = _kernelTestHelper.GenerateBlock(10, HashHelper.ComputeFrom("PreviousBlockHash"),
            new List<Transaction> { transaction });

        block.Header.Signature =
            ByteString.CopyFrom(CryptoHelper.SignWithPrivateKey(_kernelTestHelper.KeyPair.PrivateKey,
                block.GetHash().ToByteArray()));

        return block;
    }
}