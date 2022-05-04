﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassesManagerReborn.Patchs
{
    [Serializable]
    [HarmonyPatch(typeof(ModdingUtils.Utils.Cards), "PlayerIsAllowedCard")]
    public class PlayerIsAllowedCard
    {
        public static void Postfix(ref bool __result, Player player, CardInfo card)
        {
            if (card == Cards.JACK.card || card == Cards.MasteringTrade.card)
                if (ClassesRegistry.GetClassObjects(~CardType.Entry).Count == 0)
                    __result = false;
            if (!__result) return;
            if (ClassesRegistry.Registry.ContainsKey(card))
            {
                __result = ClassesRegistry.Registry[card].PlayerIsAllowedCard(player);
            }else if(card == Cards.JACK.card)
            {
                __result = !player.data.currentCards.Intersect(ClassesRegistry.GetClassInfos(CardType.Entry)).Any();
            }else if (card == Cards.MasteringTrade.card)
            {
                __result = player.data.currentCards.Intersect(ClassesRegistry.GetClassInfos(CardType.Entry)).Any();
            }
        }
    }
}
