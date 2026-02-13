using DrawnIntoDarkness.Cards;
using DrawnIntoDarkness.Cards.ActionCards.StatusEffect;
using DrawnIntoDarkness.Cards.DefenseModifierCards;
using DrawnIntoDarkness.Decks;
using DrawnIntoDarkness.Entities;
using DrawnIntoDarkness.Managers;
using System.Collections.Generic;
using static DrawnIntoDarkness.Helpers.Enums.GeneralEnums;

namespace DrawnIntoDarkness.Cards.LootCards.Armor
{
    public sealed class DawnbringerArmor : EquipmentCardBase, IGrantStatusesOnBattleStart
    {
        private readonly List<ModifierEntry> _entries = new();
        private readonly int _shield;

        public DawnbringerArmor() : this(false, 6) { }

        public DawnbringerArmor(bool veiled = false, int shield = 6)
            : base(
                name: "Dawnbringer Armor",
                description: "Adds a +3 Block defense card and start each battle with Aegis 6.",
                rarity: Rarity.Epic,
                slot: EquipmentSlot.Armor,
                twoHanded: false,
                veiled: veiled,
                lootType: LootTypes.Equipment)
        {
            _shield = shield;
        }

        protected override void OnEquip(ModifierManager mods)
        {
            if (_entries.Count > 0) return;

            _entries.Add(mods.AddAdd(
                DeckTarget.Defense,
                ModifierScope.Permanent,
                DefenseModifierCard.Block(3)));
        }

        protected override void OnUnequip(ModifierManager mods)
        {
            foreach (var entry in _entries)
                mods.Remove(entry);
            _entries.Clear();
        }

        public IEnumerable<IStatusEffect> GetStatusesFor(IEntity bearer)
        {
            yield return new AegisStatus(_shield);
        }
    }
}
