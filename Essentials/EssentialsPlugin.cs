using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Essentials.Commands;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Managers;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Session;
using Torch.Views;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Essentials
{
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin
    {
        public EssentialsConfig Config => _config?.Data;

        private TorchSessionManager _sessionManager;

        private UserControl _control;
        private Persistent<EssentialsConfig> _config;
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private HashSet<ulong> _motdOnce = new HashSet<ulong>();

        public static EssentialsPlugin Instance { get; private set; }

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new PropertyGrid(){DataContext=Config, IsEnabled = false});

        public void Save()
        {
            _config.Save();
        }

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            string path = Path.Combine(StoragePath, "Essentials.cfg");
            Log.Info($"Attempting to load config from {path}");
            _config = Persistent<EssentialsConfig>.Load(path);
            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager.  MOTD won't work");

            Instance = this;
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            var mpMan = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>();
            switch (state)
            {
                case TorchSessionState.Loaded:
                    mpMan.PlayerJoined += MotdOnce;
                    mpMan.PlayerLeft += ResetMotdOnce;
                    MyEntities.OnEntityAdd += EntityAdded;
                    if(Config.StopShipsOnStart)
                        StopShips();
                    _control?.Dispatcher.Invoke(() =>
                                               {
                                                   _control.IsEnabled = true;
                                                   _control.DataContext = Config;
                                               });
                    AutoCommands.Instance.Start();
                    InfoModule.Init();
                    break;
                case TorchSessionState.Unloading:
                    mpMan.PlayerLeft -= ResetMotdOnce;
                    mpMan.PlayerJoined -= MotdOnce;
                    MyEntities.OnEntityAdd -= EntityAdded;
                    _bagTracker.Clear();
                    _removalTracker.Clear();
                    break;
            }
        }

        private Dictionary<long, List<MyInventoryBagEntity>> _bagTracker = new Dictionary<long, List<MyInventoryBagEntity>>();
        private Queue<Tuple<MyInventoryBagEntity, DateTime>> _removalTracker = new Queue<Tuple<MyInventoryBagEntity, DateTime>>();

        private void EntityAdded(MyEntity myEntity)
        {
            if (Config.BackpackLimit < 0)
                return;

            var b = myEntity as MyInventoryBagEntity;
            if(b == null)
                return;
            
            if (Config.BackpackLimit == 0)
            { 
                _removalTracker.Enqueue(new Tuple<MyInventoryBagEntity, DateTime>(b, DateTime.Now + TimeSpan.FromSeconds(30)));
                return;
            }

            if (!_bagTracker.TryGetValue(b.OwnerIdentityId, out List<MyInventoryBagEntity> bags))
            {
                bags = new List<MyInventoryBagEntity>(Config.BackpackLimit);
                _bagTracker.Add(b.OwnerIdentityId, bags);
            }

            bags.Add(b);
        }

        private void ProcessBags()
        { 
            //bags don't have inventory in the Add event, so we wait until the next tick. I hate everything.
            foreach (var bags in _bagTracker.Values)
            {
                //iterate backwards so we can remove while we iterate
                for (int i = bags.Count - 1; i > 0; i--)
                {
                    var b = bags[i];
                    if (b.GetInventory().GetItemsCount() == 0)
                    {
                        _removalTracker.Enqueue(new Tuple<MyInventoryBagEntity, DateTime>(b, DateTime.Now + TimeSpan.FromSeconds(30)));
                        bags.RemoveAt(i);
                    }
                }
                //lazy
                while (bags.Count > Config.BackpackLimit)
                {
                    var rm = bags[0];
                    bags.RemoveAt(0);
                    _removalTracker.Enqueue(new Tuple<MyInventoryBagEntity, DateTime>(rm, DateTime.Now + TimeSpan.FromSeconds(30)));
                }
            }
            if (_removalTracker.Count > 0)
            {
                var b = _removalTracker.Peek();
                if (DateTime.Now >= b.Item2)
                {
                    _removalTracker.Dequeue();
                    b.Item1.Close();
                }
            }
        }

        public override void Update()
        {
            ProcessBags();
        }

        private void StopShips()
        {
            foreach (var e in MyEntities.GetEntities())
            {
                e.Physics?.SetSpeeds(Vector3.Zero, Vector3.Zero);
            }
        }

        private void ResetMotdOnce(IPlayer player)
        {
            _motdOnce.Remove(player.SteamId);
        }

        private void MotdOnce(IPlayer player)
        {
            //TODO: REMOVE ALL THIS TRASH!
            //implement a PlayerSpawned event in Torch. This will work for now.
            Task.Run(() =>
                     {
                         var start = DateTime.Now;
                         var timeout = TimeSpan.FromMinutes(5);
                         var pid = new MyPlayer.PlayerId(player.SteamId, 0);
                         while (DateTime.Now - start <= timeout)
                         {
                             if (!MySession.Static.Players.TryGetPlayerById(pid, out MyPlayer p) || p.Character == null)
                             {
                                 Thread.Sleep(1000);
                                 continue;
                             }

                             Torch.Invoke(() =>
                                          {
                                              if (_motdOnce.Contains(player.SteamId))
                                                  return;

                                              SendMotd(p);
                                              _motdOnce.Add(player.SteamId);
                                          });
                             break;
                         }
                     });
        }

        public void SendMotd(MyPlayer player)
        {
            long playerId = player.Identity.IdentityId;
            if (!string.IsNullOrEmpty(Config.MotdUrl))
            {
                if (MyGuiSandbox.IsUrlWhitelisted(Config.MotdUrl))
                    MyVisualScriptLogicProvider.OpenSteamOverlay(Config.MotdUrl, playerId);
                else
                    MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={Config.MotdUrl}", playerId);
            }

            var id = player.Client.SteamUserId;
            if (id <= 0) //can't remember if this returns 0 or -1 on error.
                return;
            
            string name = player.Identity?.DisplayName ?? "player";

            bool newUser = !Config.KnownSteamIds.Contains(id);
            if (newUser)
                Config.KnownSteamIds.Add(id);

            if (newUser && !string.IsNullOrEmpty(Config.NewUserMotd))
            {
                ModCommunication.SendMessageTo(new DialogMessage(MySession.Static.Name, "New User Message Of The Day", Config.NewUserMotd.Replace("%player%", name)), id);
            }
            else if (!string.IsNullOrEmpty(Config.Motd))
            {
                ModCommunication.SendMessageTo(new DialogMessage(MySession.Static.Name, "Message Of The Day", Config.Motd.Replace("%player%", name)), id);
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged -= SessionChanged;
            _sessionManager = null;
        }
    }
}
