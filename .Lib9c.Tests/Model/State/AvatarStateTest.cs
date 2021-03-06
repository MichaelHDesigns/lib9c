namespace Lib9c.Tests.Model.State
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Bencodex;
    using Bencodex.Types;
    using Libplanet;
    using Libplanet.Crypto;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.Quest;
    using Nekoyume.Model.State;
    using Xunit;

    public class AvatarStateTest
    {
        private readonly TableSheets _tableSheets;

        public AvatarStateTest()
        {
            var sheets = TableSheetsImporter.ImportSheets();
            _tableSheets = new TableSheets(sheets);
        }

        [Fact]
        public void Serialize()
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);

            var serialized = avatarState.Serialize();
            var deserialized = new AvatarState((Bencodex.Types.Dictionary)serialized);

            Assert.Equal(avatarState.address, deserialized.address);
            Assert.Equal(avatarState.agentAddress, deserialized.agentAddress);
            Assert.Equal(avatarState.blockIndex, deserialized.blockIndex);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        public async Task ConstructDeterministic(int waitMilliseconds)
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            AvatarState avatarStateA = GetNewAvatarState(avatarAddress, agentAddress);
            await Task.Delay(waitMilliseconds);
            AvatarState avatarStateB = GetNewAvatarState(avatarAddress, agentAddress);

            HashDigest<SHA256> Hash(AvatarState avatarState) => Hashcash.Hash(new Codec().Encode(avatarState.Serialize()));
            Assert.Equal(Hash(avatarStateA), Hash(avatarStateB));
        }

        [Fact]
        public void UpdateFromQuestRewardDeterministic()
        {
            var rankingState = new RankingState();
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = new AvatarState(
                avatarAddress,
                agentAddress,
                0,
                _tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                rankingState.UpdateRankingMap(avatarAddress));
            var itemIds = avatarState.questList.OfType<ItemTypeCollectQuest>().First().ItemIds;
            var map = new Dictionary<int, int>()
            {
                [400000] = 1,
                [302002] = 1,
                [302003] = 1,
                [302001] = 1,
                [306023] = 1,
                [302000] = 1,
            };

            var serialized = (Dictionary)avatarState.questList.OfType<WorldQuest>().First().Serialize();
            serialized = serialized.SetItem("reward", new Nekoyume.Model.Quest.QuestReward(map).Serialize());

            var quest = new WorldQuest(serialized);

            avatarState.UpdateFromQuestReward(quest, _tableSheets.MaterialItemSheet);
            Assert.Equal(
                avatarState.questList.OfType<ItemTypeCollectQuest>().First().ItemIds,
                new List<int>()
                {
                    302000,
                    302001,
                    302002,
                    302003,
                    306023,
                }
            );
        }

        [Theory]
        [InlineData(1, GameConfig.RequireCharacterLevel.CharacterConsumableSlot1)]
        [InlineData(2, GameConfig.RequireCharacterLevel.CharacterConsumableSlot2)]
        [InlineData(3, GameConfig.RequireCharacterLevel.CharacterConsumableSlot3)]
        [InlineData(4, GameConfig.RequireCharacterLevel.CharacterConsumableSlot4)]
        [InlineData(5, GameConfig.RequireCharacterLevel.CharacterConsumableSlot5)]
        public void ValidateConsumable(int count, int level)
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = level;

            var consumableIds = new List<Guid>();
            var row = _tableSheets.ConsumableItemSheet.Values.First();
            for (var i = 0; i < count; i++)
            {
                var id = Guid.NewGuid();
                var consumable = ItemFactory.CreateItemUsable(row, id, 0);
                consumableIds.Add(id);
                avatarState.inventory.AddItem(consumable);
            }

            avatarState.ValidateConsumable(consumableIds, 0);
        }

        [Fact]
        public void ValidateConsumableThrowRequiredBlockIndexException()
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);

            var consumableIds = new List<Guid>();
            var row = _tableSheets.ConsumableItemSheet.Values.First();
            var id = Guid.NewGuid();
            var consumable = ItemFactory.CreateItemUsable(row, id, 1);
            consumableIds.Add(id);
            avatarState.inventory.AddItem(consumable);
            Assert.Throws<RequiredBlockIndexException>(() => avatarState.ValidateConsumable(consumableIds, 0));
        }

        [Fact]
        public void ValidateConsumableThrowConsumableSlotOutOfRangeException()
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = GameConfig.RequireCharacterLevel.CharacterConsumableSlot5;

            var consumableIds = new List<Guid>();
            var row = _tableSheets.ConsumableItemSheet.Values.First();
            for (var i = 0; i < 6; i++)
            {
                var id = Guid.NewGuid();
                var consumable = ItemFactory.CreateItemUsable(row, id, 0);
                consumableIds.Add(id);
                avatarState.inventory.AddItem(consumable);
            }

            Assert.Throws<ConsumableSlotOutOfRangeException>(() => avatarState.ValidateConsumable(consumableIds, 0));
        }

        [Theory]
        [InlineData(1, GameConfig.RequireCharacterLevel.CharacterConsumableSlot1)]
        [InlineData(2, GameConfig.RequireCharacterLevel.CharacterConsumableSlot2)]
        [InlineData(3, GameConfig.RequireCharacterLevel.CharacterConsumableSlot3)]
        [InlineData(4, GameConfig.RequireCharacterLevel.CharacterConsumableSlot4)]
        [InlineData(5, GameConfig.RequireCharacterLevel.CharacterConsumableSlot5)]
        public void ValidateConsumableSlotThrowConsumableSlotUnlockException(int count, int level)
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = level - 1;

            var consumableIds = new List<Guid>();
            var row = _tableSheets.ConsumableItemSheet.Values.First();
            for (var i = 0; i < count; i++)
            {
                var id = Guid.NewGuid();
                var consumable = ItemFactory.CreateItemUsable(row, id, 0);
                consumableIds.Add(id);
                avatarState.inventory.AddItem(consumable);
            }

            Assert.Throws<ConsumableSlotUnlockException>(() => avatarState.ValidateConsumable(consumableIds, 0));
        }

        [Fact]
        public void ValidateCostume()
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = 100;

            var costumeIds = new HashSet<int>();
            var subTypes = new[]
            {
                ItemSubType.FullCostume,
                ItemSubType.HairCostume,
                ItemSubType.EarCostume,
                ItemSubType.EyeCostume,
                ItemSubType.TailCostume,
                ItemSubType.Title,
            };
            foreach (var subType in subTypes)
            {
                var row = _tableSheets.CostumeItemSheet.Values.First(r => r.ItemSubType == subType);
                var costume = ItemFactory.CreateCostume(row);
                costumeIds.Add(costume.Id);
                avatarState.inventory.AddItem(costume);
            }

            avatarState.ValidateCostume(costumeIds);
        }

        [Theory]
        [InlineData(ItemSubType.FullCostume)]
        [InlineData(ItemSubType.HairCostume)]
        [InlineData(ItemSubType.EarCostume)]
        [InlineData(ItemSubType.EyeCostume)]
        [InlineData(ItemSubType.TailCostume)]
        [InlineData(ItemSubType.Title)]
        public void ValidateCostumeThrowDuplicateCostumeException(ItemSubType type)
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = 100;

            var costumeIds = new HashSet<int>();
            var duplicateRows = _tableSheets.CostumeItemSheet.Values.Where(r => r.ItemSubType == type);
            var row = _tableSheets.CostumeItemSheet.Values.First(r => r.ItemSubType != type);
            var costume = ItemFactory.CreateCostume(row);
            costumeIds.Add(costume.Id);
            avatarState.inventory.AddItem(costume);

            foreach (var duplicateRow in duplicateRows)
            {
                var duplicateCostume = ItemFactory.CreateCostume(duplicateRow);
                costumeIds.Add(duplicateCostume.Id);
                avatarState.inventory.AddItem(duplicateCostume);
            }

            Assert.Throws<DuplicateCostumeException>(() => avatarState.ValidateCostume(costumeIds));
        }

        [Fact]
        public void ValidateCostumeThrowInvalidItemTypeException()
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = 100;

            var row = _tableSheets.CostumeItemSheet.Values.First();
            var costume = ItemFactory.CreateCostume(row);
            var serialized = (Dictionary)costume.Serialize();
            serialized = serialized.SetItem("item_sub_type", ItemSubType.Armor.Serialize());
            var costume2 = new Costume(serialized);
            var costumeIds = new HashSet<int> { costume2.Id };
            avatarState.inventory.AddItem(costume2);

            Assert.Throws<InvalidItemTypeException>(() => avatarState.ValidateCostume(costumeIds));
        }

        [Theory]
        [InlineData(ItemSubType.FullCostume, GameConfig.RequireCharacterLevel.CharacterFullCostumeSlot)]
        [InlineData(ItemSubType.HairCostume, GameConfig.RequireCharacterLevel.CharacterHairCostumeSlot)]
        [InlineData(ItemSubType.EarCostume, GameConfig.RequireCharacterLevel.CharacterEarCostumeSlot)]
        [InlineData(ItemSubType.EyeCostume, GameConfig.RequireCharacterLevel.CharacterEyeCostumeSlot)]
        [InlineData(ItemSubType.TailCostume, GameConfig.RequireCharacterLevel.CharacterTailCostumeSlot)]
        [InlineData(ItemSubType.Title, GameConfig.RequireCharacterLevel.CharacterTitleSlot)]
        public void ValidateCostumeThrowCostumeSlotUnlockException(ItemSubType type, int level)
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            Address agentAddress = new PrivateKey().ToAddress();
            var avatarState = GetNewAvatarState(avatarAddress, agentAddress);
            avatarState.level = level - 1;

            var row = _tableSheets.CostumeItemSheet.Values.First(r => r.ItemSubType == type);
            var costume = ItemFactory.CreateCostume(row);
            var costumeIds = new HashSet<int> { costume.Id };
            avatarState.inventory.AddItem(costume);

            Assert.Throws<CostumeSlotUnlockException>(() => avatarState.ValidateCostume(costumeIds));
        }

        private AvatarState GetNewAvatarState(Address avatarAddress, Address agentAddress)
        {
            var rankingState = new RankingState();
            return new AvatarState(
                avatarAddress,
                agentAddress,
                0,
                _tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                rankingState.UpdateRankingMap(avatarAddress));
        }
    }
}
