﻿using System;
using System.Collections.Generic;
using System.Text;
using UnboundLib.Cards;
using UnityEngine;
using ModdingUtils.Extensions;
using UnboundLib;
using System.Linq;
using UnityEngine.UI;

namespace ClassesManagerReborn.Cards
{
    internal class JACK : CustomCard
    {
        internal static CardInfo card;
        public override void SetupCard(CardInfo cardInfo, Gun gun, ApplyCardStats cardStats, CharacterStatModifiers statModifiers)
        {
            gameObject.AddComponent<legend>();
            cardInfo.allowMultiple = false;
            cardInfo.GetAdditionalData().canBeReassigned = false;
        }
        public override void OnAddCard(Player player, Gun gun, GunAmmo gunAmmo, CharacterData data, HealthHandler health, Gravity gravity, Block block, CharacterStatModifiers characterStats)
        {
            ClassesManager.instance.ExecuteAfterFrames(2, () => { 
                List<CardInfo> classes = ClassesRegistry.GetClassInfos(CardType.Entry).Intersect(ModdingUtils.Utils.Cards.active).ToList();
                foreach (CardInfo card in classes)
                {
                    if(!player.data.currentCards.Contains(card))
                        ModdingUtils.Utils.Cards.instance.AddCardToPlayer(player, card, addToCardBar: true);
                }
            });
        }

        protected override GameObject GetCardArt()
        {
            return null;
        }

        protected override string GetDescription()
        {
            return "Master of none";
        }

        protected override CardInfo.Rarity GetRarity()
        {
            return CardInfo.Rarity.Rare;
        }

        protected override CardInfoStat[] GetStats() 
        {
            return new CardInfoStat[] { };
        }

        protected override CardThemeColor.CardThemeColorType GetTheme()
        {
            return CardThemeColor.CardThemeColorType.NatureBrown;
        }

        protected override string GetTitle()
        {
            return "JACK";
        }

        public override string GetModName()
        {
            return "CMR";
        }

    }
    internal class legend : MonoBehaviour
    {
        public void Update()
        {
            FindObjectsInChildren(gameObject, "Triangle").ForEach(t => t.GetComponent<Image>().color = new Color(1, 1, 0, 1));
        }

        public static List<GameObject> FindObjectsInChildren(GameObject gameObject, string gameObjectName)
        {
            List<GameObject> returnObjects = new List<GameObject>();
            Transform[] children = gameObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in children)
            {
                if (item.gameObject.name.Equals(gameObjectName))
                {
                    returnObjects.Add(item.gameObject);
                }
            }

            return returnObjects;
        }
    }
}
