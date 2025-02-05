﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnboundLib;
using UnboundLib.Networking;
using UnboundLib.Utils.UI;
using UnityEngine;
using Photon.Pun;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnboundLib.GameModes;
using ClassesManagerReborn.Util;
using UnboundLib.Cards;

namespace ClassesManagerReborn
{
    // These are the mods required for our Mod to work
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("pykess.rounds.plugins.moddingutils", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("pykess.rounds.plugins.cardchoicespawnuniquecardpatch", BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility("fluxxfield.rounds.plugins.classesmanager")]
    // Declares our Mod to Bepin
    [BepInPlugin(ModId, ModName, Version)]
    // The game our Mod Is associated with
    [BepInProcess("Rounds.exe")]
    public class ClassesManager : BaseUnityPlugin
    {
        private const string ModId = "root.classes.manager.reborn";
        private const string ModName = "Classes Manager Reborn";
        public const string Version = "1.4.3";
        public const string ModInitials = "CMR";

        public static ClassesManager instance { get; private set; }

        public static ConfigEntry<bool> DEBUG;
        public static ConfigEntry<bool> Force_Class;
        public static ConfigEntry<bool> Ignore_Blacklist;
        public static ConfigEntry<bool> Ensure_Class_Card;
        public static ConfigEntry<bool> Class_War;
        public static ConfigEntry<float> Class_Odds;

        internal static bool firstHand = true;
        internal static int cardsToDraw = 0;
        internal static AssetBundle assets;

        internal System.Collections.IEnumerator InstantiateModClasses()
        {
            for (int _ = 0; _ < 5; _++) yield return null;
            List<Task> tasks = new List<Task>();
            PluginInfo[] pluginInfos = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Where(pi => pi.Dependencies.Any(d => d.DependencyGUID == ModId)).ToArray();
            List<ClassHandler> handlers = new List<ClassHandler>();
            foreach (PluginInfo info in pluginInfos)
            {
                Assembly mod = Assembly.LoadFile(info.Location);
                Type[] types = mod.GetTypes().Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(ClassHandler))).ToArray();
                foreach (Type type in types)
                {
                    ClassHandler handler = (ClassHandler)Activator.CreateInstance(type);
                    handlers.Add(handler);
                    tasks.Add(new Task(handler.Init(), name: type.FullName));
                }
                
            }
            int counter = 0;
            while (tasks.Any(t => t.Running)) { 
                if(++counter == 20)foreach (Task task in tasks.Where(t => t.Running)) 
                        UnityEngine.Debug.LogWarning($"ClassHanlder {task.Name} Init function is taking a long time to finish, this could be a bug.");
                if (counter == 200) foreach (Task task in tasks.Where(t => t.Running))
                    {
                        task.Stop();
                        UnityEngine.Debug.LogError($"ClassHanlder {task.Name} Init function has Faild, Skipping. This will cause unintended game play behavoir.");
                    }
                yield return null;  
            }
            tasks.Clear();
            foreach (ClassHandler h in handlers) tasks.Add(new Task(h.PostInit(), name: h.GetType().FullName));
            counter = 0;
            while (tasks.Any(t => t.Running)) { 
                if (++counter == 20) foreach (Task task in tasks.Where(t => t.Running)) 
                        UnityEngine.Debug.LogWarning($"ClassHanlder {task.Name} PostInit function is taking a long time to finish, this could be a bug.");
                if (counter == 200) foreach (Task task in tasks.Where(t => t.Running))
                    {
                        task.Stop();
                        UnityEngine.Debug.LogError($"ClassHanlder {task.Name} PostInit function has Faild, Skipping. This will cause unintended game play behavoir.");
                    }
                yield return null;
            }
            Debug("Class Setup Complete", true);
        }

        void Awake()
        {
            DEBUG = base.Config.Bind<bool>(ModId, "Debug", false, "Enable to turn on concole spam from our mod");


            var harmony = new Harmony(ModId);
            harmony.PatchAll();


            Force_Class = base.Config.Bind<bool>(ModId, "Force_Class", false, "Enable Force Classes");
            Ignore_Blacklist = base.Config.Bind<bool>(ModId, "Ignore_Blacklist", false, "Allow more then one class per player");
            Ensure_Class_Card = base.Config.Bind<bool>(ModId, "Ensure_Class_Card", false, "Guarantee players in a class will draw a card for that class if able");
            Class_War = base.Config.Bind<bool>(ModId, "Class_War", false, "Prevent players from having the same class");
            Class_Odds = base.Config.Bind<float>(ModId, "Class_Odds", 1f, "Incresses the chances of a class restricted card to show up (Intended for large packs)");
        }

        void Start()
        {
            instance = this;

            assets = Jotunn.Utils.AssetUtils.LoadAssetBundleFromResources("cmr", typeof(ClassesManager).Assembly);

            Unbound.RegisterHandshake(ModId, this.OnHandShakeCompleted);

            Unbound.RegisterMenu(ModName, delegate () { }, new Action<GameObject>(this.NewGUI), null, false);


            //instance.ExecuteAfterFrames(10, TestMode);
            instance.StartCoroutine(InstantiateModClasses());

            GameModeManager.AddHook(GameModeHooks.HookGameStart, OnGameStart);
            GameModeManager.AddHook(GameModeHooks.HookPlayerPickEnd, CleanupClasses);
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, CleanupClasses);
            GameModeManager.AddHook(GameModeHooks.HookGameStart, Patchs.AjustRarities.gameStart);

            CustomCard.BuildCard<Cards.JACK>(card => Cards.JACK.card = card);
            CustomCard.BuildCard<Cards.MasteringTrade>(card => Cards.MasteringTrade.card = card);
        }

        private void OnHandShakeCompleted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC_Others(typeof(ClassesManager), nameof(SyncSettings), new object[] { Force_Class.Value, Ignore_Blacklist.Value, Ensure_Class_Card.Value, Class_War.Value, Class_Odds.Value });
            }
        }

        [UnboundRPC]
        private static void SyncSettings(bool host_Force_Class, bool host_Ignore_Blacklist, bool host_Ensure_Class_Card, bool host_Class_War, float host_Double_Odds)
        {
            Force_Class.Value = host_Force_Class;
            Ignore_Blacklist.Value = host_Ignore_Blacklist;
            Ensure_Class_Card.Value = host_Ensure_Class_Card;
            Class_War.Value = host_Class_War;
            Class_Odds.Value = host_Double_Odds;
        }

        private void NewGUI(GameObject menu)
        {
            MenuHandler.CreateText(ModName, menu, out _, 60, false, null, null, null, null);

            MenuHandler.CreateToggle(Force_Class.Value, "Enable Force Classes", menu, value => Force_Class.Value = value);
            MenuHandler.CreateToggle(Ignore_Blacklist.Value, "Allow more then one class and subclass per player", menu, value => Ignore_Blacklist.Value = value);
            MenuHandler.CreateToggle(Ensure_Class_Card.Value, "Guarantee players in a class will draw a card for that class if able", menu, value => Ensure_Class_Card.Value = value);
            MenuHandler.CreateToggle(Class_War.Value, "Prevent players from having the same class", menu, value => Class_War.Value = value);
            MenuHandler.CreateSlider("Increase the chances of a class restricted card to show up (Intended for large packs)", menu, 30, 1, 100, Class_Odds.Value, value => Class_Odds.Value = value, out UnityEngine.UI.Slider _);


            MenuHandler.CreateText("", menu, out _);
            MenuHandler.CreateToggle(DEBUG.Value, "Debug Mode", menu, value => DEBUG.Value = value, 50, false, Color.red, null, null, null);
        }


        public static void Debug(object message, bool important = false)
        {
            if (DEBUG.Value || important)
            {
                UnityEngine.Debug.Log($"{ModInitials}=>{message}");
            }
        }

        public static System.Collections.IEnumerator OnGameStart(IGameModeHandler gm)
        {
            Cards.MasteringTrade.masteringPlayers = new List<Player>();
            firstHand = true;
            cardsToDraw = 0;
            yield break;
        }

        public static System.Collections.IEnumerator CleanupClasses(IGameModeHandler gm)
        {
            firstHand = false;
            List<Task> tasks = new List<Task>();
            if (Cards.MasteringTrade.masteringPlayers.Count > 0)
            {
                foreach (var player in Cards.MasteringTrade.masteringPlayers)
                {
                    tasks.Add(new Task(Cards.MasteringTrade.IAddClassCards(player)));
                }
                Cards.MasteringTrade.masteringPlayers.Clear();
            }
            while (tasks.Any(t => t.Running)) yield return null;
            tasks.Clear();

            foreach (Player player in PlayerManager.instance.players)
            {
                tasks.Add(new Task(CleanupClassCards(player)));
            }
            while (tasks.Any(t => t.Running)) yield return null;
            yield break;
        }

        internal static System.Collections.IEnumerator CleanupClassCards(Player player)
        {
            List<CardInfo> cards = player.data.currentCards.ToList();
            bool eddited = false;
            for(int i = 0; i < cards.Count; ++i)
            {
                if (ClassesRegistry.Registry.ContainsKey(cards[i]) && (ClassesRegistry.Registry[cards[i]].type & CardType.NonClassCard) == 0)
                {
                    CardInfo? requriment = ClassesRegistry.Registry[cards[i]].GetMissingClass(player);
                    if (requriment != null)
                    {
                        cards[i] = requriment;
                        eddited = true;
                        i = 0;
                    }
                }
            }
            if (eddited)
            {
                ModdingUtils.Utils.Cards.instance.RemoveAllCardsFromPlayer(player);
                ModdingUtils.Utils.Cards.instance.AddCardsToPlayer(player, cards.ToArray(), true, addToCardBar: true);
            }
            yield break;
        }

        /// <summary>
        /// Generates fake classes using vanilla cards for testing purposes 
        /// </summary>
        private static void TestMode()
        {
            ModdingUtils.Utils.Cards cards = ModdingUtils.Utils.Cards.instance;

            ClassesRegistry.Register(cards.GetCardWithName("DEFENDER"), CardType.Entry);
            ClassesRegistry.Register(cards.GetCardWithName("ECHO"), CardType.Card, cards.GetCardWithName("DEFENDER"));
            ClassesRegistry.Register(cards.GetCardWithName("FROST SLAM"), CardType.Card, cards.GetCardWithName("DEFENDER"));
            ClassesRegistry.Register(cards.GetCardWithName("Healing field"), CardType.Card, cards.GetCardWithName("DEFENDER"));
            ClassesRegistry.Register(cards.GetCardWithName("SHIELDS UP"), CardType.Card, cards.GetCardWithName("DEFENDER"));
            ClassesRegistry.Register(cards.GetCardWithName("SHIELD CHARGE"), CardType.Card, cards.GetCardWithName("DEFENDER"));
            ClassesRegistry.Register(cards.GetCardWithName("STATIC FIELD"), CardType.Card, cards.GetCardWithName("DEFENDER"));

            ClassesRegistry.Register(cards.GetCardWithName("BOUNCY"), CardType.Entry);
            ClassesRegistry.Register(cards.GetCardWithName("TRICKSTER"), CardType.Card, cards.GetCardWithName("BOUNCY"));
            ClassesRegistry.Register(cards.GetCardWithName("RICCOCHET"), CardType.Card, cards.GetCardWithName("BOUNCY"));
            ClassesRegistry.Register(cards.GetCardWithName("MAYHEM"), CardType.Card, cards.GetCardWithName("BOUNCY"));
            ClassesRegistry.Register(cards.GetCardWithName("Target BOUNCE"), CardType.Card, cards.GetCardWithName("BOUNCY"));

            ClassesRegistry.Register(cards.GetCardWithName("BRAWLER"), CardType.Entry);
            ClassesRegistry.Register(cards.GetCardWithName("Refresh"), CardType.Card, cards.GetCardWithName("BRAWLER"));
            ClassesRegistry.Register(cards.GetCardWithName("SCAVENGER"), CardType.Card, cards.GetCardWithName("BRAWLER"));
            ClassesRegistry.Register(cards.GetCardWithName("CHASE"), CardType.Card, cards.GetCardWithName("BRAWLER"));
            ClassesRegistry.Register(cards.GetCardWithName("DECAY"), CardType.Card, cards.GetCardWithName("BRAWLER"));
            ClassesRegistry.Register(cards.GetCardWithName("RADIANCE"), CardType.Card, cards.GetCardWithName("BRAWLER"));

            ClassesRegistry.Register(cards.GetCardWithName("LIFESTEALER"), CardType.Entry);
            ClassesRegistry.Register(cards.GetCardWithName("PARASITE"), CardType.Card, cards.GetCardWithName("LIFESTEALER"));
            ClassesRegistry.Register(cards.GetCardWithName("LEECH"), CardType.Card, cards.GetCardWithName("LIFESTEALER"));
            ClassesRegistry.Register(cards.GetCardWithName("POISON"), CardType.Card, cards.GetCardWithName("LIFESTEALER"));
            ClassesRegistry.Register(cards.GetCardWithName("DAZZLE"), CardType.Card, cards.GetCardWithName("LIFESTEALER"));
            ClassesRegistry.Register(cards.GetCardWithName("TASTE OF BLOOD"), CardType.Card, cards.GetCardWithName("LIFESTEALER"));

            ClassesRegistry.Register(cards.GetCardWithName("Demonic pact"), CardType.Entry);
            ClassesRegistry.Register(cards.GetCardWithName("TOXIC CLOUD"), CardType.Card, cards.GetCardWithName("Demonic pact"));
            ClassesRegistry.Register(cards.GetCardWithName("SILENCE"), CardType.Card, cards.GetCardWithName("Demonic pact"));
            ClassesRegistry.Register(cards.GetCardWithName("ABYSSAL COUNTDOWN"), CardType.Card, cards.GetCardWithName("Demonic pact"));
            ClassesRegistry.Register(cards.GetCardWithName("GROW"), CardType.Card, cards.GetCardWithName("Demonic pact"));
            ClassesRegistry.Register(cards.GetCardWithName("PHOENIX"), CardType.Card, cards.GetCardWithName("Demonic pact"));

            ClassesRegistry.Register(cards.GetCardWithName("EMPOWER"), CardType.Entry);
            ClassesRegistry.Register(cards.GetCardWithName("EMP"), CardType.Card, cards.GetCardWithName("EMPOWER"));
            ClassesRegistry.Register(cards.GetCardWithName("REMOTE"), CardType.Card, cards.GetCardWithName("EMPOWER"));
            ClassesRegistry.Register(cards.GetCardWithName("SNEAKY"), CardType.Card, cards.GetCardWithName("EMPOWER"));
            ClassesRegistry.Register(cards.GetCardWithName("THRUSTER"), CardType.Card, cards.GetCardWithName("EMPOWER"));
            ClassesRegistry.Register(cards.GetCardWithName("HOMING"), CardType.Card, cards.GetCardWithName("EMPOWER"));
        }

    }
}
