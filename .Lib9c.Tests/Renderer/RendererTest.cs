﻿namespace Lib9c.Tests.Renderer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Security.Cryptography;
    using Lib9c.Renderer;
    using Lib9c.Tests.Action;
    using Libplanet;
    using Libplanet.Action;
    using Libplanet.Blocks;
    using Libplanet.Crypto;
    using Libplanet.Tx;
    using Nekoyume.Action;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;
    using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
    using NCBlock = Libplanet.Blocks.Block<Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>>;

    public class RendererTest
    {
        public RendererTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<RendererTest>();
        }

        [Fact]
        public void BlockRendererTest()
        {
            var blockRenderer = new BlockRenderer();
            var branchPointBlock = new Block<NCAction>(
                index: 9,
                difficulty: 10000,
                totalDifficulty: 90000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x01 }),
                miner: default,
                previousHash: null,
                timestamp: DateTimeOffset.MinValue,
                transactions: Enumerable.Empty<Transaction<NCAction>>());
            var oldBlock = new Block<NCAction>(
                index: 10,
                difficulty: 10000,
                totalDifficulty: 100000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x02 }),
                miner: default,
                previousHash: branchPointBlock.Hash,
                timestamp: DateTimeOffset.MinValue.AddSeconds(1),
                transactions: Enumerable.Empty<Transaction<NCAction>>());
            var newBlock = new Block<NCAction>(
                index: 10,
                difficulty: 10000,
                totalDifficulty: 100000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x03 }),
                miner: default,
                previousHash: branchPointBlock.Hash,
                timestamp: DateTimeOffset.MinValue.AddSeconds(2),
                transactions: Enumerable.Empty<Transaction<NCAction>>());

            (NCBlock OldTip, NCBlock NewTip) everyBlockResult = (null, null);
            (NCBlock OldTip, NCBlock NewTip, NCBlock BranchPoint) everyReorgResult = (null, null, null);
            (NCBlock OldTip, NCBlock NewTip, NCBlock BranchPoint) everyReorgEndResult = (null, null, null);

            blockRenderer.EveryBlock().Subscribe(pair => everyBlockResult = pair);
            blockRenderer.EveryReorg().Subscribe(ev => everyReorgResult = ev);
            blockRenderer.EveryReorgEnd().Subscribe(ev => everyReorgEndResult = ev);

            blockRenderer.RenderBlock(branchPointBlock, oldBlock);
            blockRenderer.RenderReorg(oldBlock, newBlock, branchPointBlock);
            blockRenderer.RenderReorgEnd(oldBlock, newBlock, branchPointBlock);

            Assert.Equal(branchPointBlock, everyBlockResult.OldTip);
            Assert.Equal(oldBlock, everyBlockResult.NewTip);
            Assert.Equal(oldBlock, everyReorgResult.OldTip);
            Assert.Equal(newBlock, everyReorgResult.NewTip);
            Assert.Equal(branchPointBlock, everyReorgResult.BranchPoint);
            Assert.Equal(oldBlock, everyReorgEndResult.OldTip);
            Assert.Equal(newBlock, everyReorgEndResult.NewTip);
            Assert.Equal(branchPointBlock, everyReorgEndResult.BranchPoint);
        }

        [Fact(Skip = "Should import IStore implementation for this test.")]
        public void ValidatingActionRendererTest()
        {
            var policy = new DebugPolicy();
            var blocks = new Dictionary<HashDigest<SHA256>, Block<NCAction>>();
            var renderer = new ValidatingActionRenderer<NCAction>();
            var branchPointBlockParent = new Block<NCAction>(
                index: 8,
                difficulty: 10000,
                totalDifficulty: 90000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x00 }),
                miner: default,
                previousHash: null,
                timestamp: DateTimeOffset.MinValue,
                transactions: Enumerable.Empty<Transaction<NCAction>>());
            var branchPointBlock = new Block<NCAction>(
                index: 9,
                difficulty: 10000,
                totalDifficulty: 90000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x01 }),
                miner: default,
                previousHash: null,
                timestamp: DateTimeOffset.MinValue.AddSeconds(1),
                transactions: Enumerable.Empty<Transaction<NCAction>>());
            var oldBlock = new Block<NCAction>(
                index: 10,
                difficulty: 10000,
                totalDifficulty: 100000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x02 }),
                miner: default,
                previousHash: branchPointBlock.Hash,
                timestamp: DateTimeOffset.MinValue.AddSeconds(2),
                transactions: Enumerable.Empty<Transaction<NCAction>>());
            var newBlock = new Block<NCAction>(
                index: 10,
                difficulty: 10000,
                totalDifficulty: 100000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x03 }),
                miner: default,
                previousHash: branchPointBlock.Hash,
                timestamp: DateTimeOffset.MinValue.AddSeconds(2),
                transactions: Enumerable.Empty<Transaction<NCAction>>());
            var privateKey = new PrivateKey();
            NCAction action1 = new HackAndSlash
            {
                costumes = new List<int>(),
                equipments = new List<Guid>(),
                foods = new List<Guid>(),
                worldId = 0,
                stageId = 1,
            };
            NCAction action2 = new HackAndSlash
            {
                costumes = new List<int>(),
                equipments = new List<Guid>(),
                foods = new List<Guid>(),
                worldId = 0,
                stageId = 2,
            };
            NCAction action3 = new HackAndSlash
            {
                costumes = new List<int>(),
                equipments = new List<Guid>(),
                foods = new List<Guid>(),
                worldId = 0,
                stageId = 3,
            };
            var tx1 = Transaction<NCAction>.Create(
                0,
                privateKey,
                null,
                new[] { action1 },
                ImmutableHashSet<Address>.Empty,
                DateTimeOffset.MinValue);
            var tx2 = Transaction<NCAction>.Create(
                1,
                privateKey,
                null,
                new[] { action2, action3 },
                ImmutableHashSet<Address>.Empty,
                DateTimeOffset.MinValue);
            var blockWithTxs = new Block<NCAction>(
                index: 11,
                difficulty: 10000,
                totalDifficulty: 100000,
                nonce: new Nonce(new byte[] { 0x00, 0x00, 0x00, 0x04 }),
                miner: default,
                previousHash: newBlock.Hash,
                timestamp: DateTimeOffset.MinValue.AddSeconds(3),
                transactions: new[] { tx1, tx2 });
            blocks.Add(branchPointBlockParent.Hash, branchPointBlockParent);
            blocks.Add(branchPointBlock.Hash, branchPointBlock);
            blocks.Add(oldBlock.Hash, oldBlock);
            blocks.Add(newBlock.Hash, newBlock);
            blocks.Add(blockWithTxs.Hash, blockWithTxs);

            var orderedActions = blockWithTxs.Transactions.SelectMany(t => t.Actions).ToArray();

            // Any of following should not throw ValidatingActionRenderer<T>.InvalidRenderException
            renderer.RenderReorg(oldBlock, blockWithTxs, branchPointBlock);
            renderer.UnrenderAction(
                new RewardGold(),
                new ActionContext { BlockIndex = oldBlock.Index },
                new State());
            renderer.RenderBlock(oldBlock, blockWithTxs);
            renderer.RenderAction(
                new RewardGold(),
                new ActionContext { BlockIndex = newBlock.Index },
                new State());
            renderer.RenderAction(
                orderedActions[0],
                new ActionContext { BlockIndex = blockWithTxs.Index },
                new State());
            renderer.RenderAction(
                orderedActions[1],
                new ActionContext { BlockIndex = blockWithTxs.Index },
                new State());
            renderer.RenderAction(
                orderedActions[2],
                new ActionContext { BlockIndex = blockWithTxs.Index },
                new State());
            renderer.RenderAction(
                new RewardGold(),
                new ActionContext { BlockIndex = blockWithTxs.Index },
                new State());
            renderer.RenderBlockEnd(oldBlock, blockWithTxs);
            renderer.RenderReorgEnd(oldBlock, blockWithTxs, branchPointBlock);
        }
    }
}
