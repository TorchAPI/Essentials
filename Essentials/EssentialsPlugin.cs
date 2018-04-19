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
using Torch.Session;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace Essentials
{
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin
    {
        public EssentialsConfig Config => _config?.Data;

        private TorchSessionManager _sessionManager;

        private EssentialsControl _control;
        private Persistent<EssentialsConfig> _config;
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private HashSet<ulong> _motdOnce = new HashSet<ulong>();

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
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            var mpMan = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>();
            switch (state)
            {
                case TorchSessionState.Loaded:
                    mpMan.PlayerLeft += ResetMotdOnce;
                    MyEntities.OnEntityAdd += MotdOnce;
                    break;
                case TorchSessionState.Unloading:
                    mpMan.PlayerLeft -= ResetMotdOnce;
                    MyEntities.OnEntityAdd -= MotdOnce;
                    break;
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
                if (MySession.Static.Players.TryGetPlayerId(playerId, out MyPlayer.PlayerId info))
                    Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()
                        .SendMessageAsOther("MOTD", Config.Motd, MyFontEnum.Blue, info.SteamId);
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