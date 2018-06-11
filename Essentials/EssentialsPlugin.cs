using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
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

        private EssentialsControl _control;
        private Persistent<EssentialsConfig> _config;
        public static readonly Logger Log = LogManager.GetLogger("Essentials");
        private HashSet<ulong> _motdOnce = new HashSet<ulong>();

        public static EssentialsPlugin Instance { get; private set; }

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new EssentialsControl(this));

        public void Save()
        {
            _config.Save();
        }

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            _config = Persistent<EssentialsConfig>.Load(Path.Combine(StoragePath, "Essentials.cfg"));
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
                    mpMan.PlayerLeft += ResetMotdOnce;
                    MyEntities.OnEntityAdd += MotdOnce;
                    if(Config.StopShipsOnStart)
                        StopShips();
                    break;
                case TorchSessionState.Unloading:
                    mpMan.PlayerLeft -= ResetMotdOnce;
                    MyEntities.OnEntityAdd -= MotdOnce;
                    break;
            }
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

        private void MotdOnce(MyEntity obj)
        {
            if (obj is MyCharacter character)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(1000);
                    Torch.Invoke(() =>
                    {
                        if (_motdOnce.Contains(character.ControlSteamId))
                            return;
                        
                        var id = character.ControllerInfo?.ControllingIdentityId;
                        if (!id.HasValue)
                            return;
                        
                        SendMotd(id.Value);
                        _motdOnce.Add(character.ControlSteamId);
                    });
                });
            }
        }

        public void SendMotd(long playerId = 0)
        {
            if (!string.IsNullOrEmpty(Config.MotdUrl))
            {
                if (MyGuiSandbox.IsUrlWhitelisted(Config.MotdUrl))
                    MyVisualScriptLogicProvider.OpenSteamOverlay(Config.MotdUrl, playerId);
                else
                    MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={Config.MotdUrl}", playerId);
            }

            if (!string.IsNullOrEmpty(Config.Motd))
            {
                var id = MySession.Static.Players.TryGetSteamId(playerId);
                if(id <= 0) //can't remember if this returns 0 or -1 on error.
                    return;
                ModCommunication.SendMessageTo(new DialogMessage(MySession.Static.Name, "Message Of The Day", Config.Motd), id);
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