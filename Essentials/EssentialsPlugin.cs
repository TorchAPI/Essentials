﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Commands;
using Torch.Managers;
using VRage.Game;
using VRage.Game.Entity;

namespace Essentials
{
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin
    {
        public EssentialsConfig Config => _config?.Data;

        private EssentialsControl _control;
        private Persistent<EssentialsConfig> _config;
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        private List<long> _motdOnce = new List<long>();

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
            Torch.SessionLoaded += Torch_SessionLoaded;
        }

        private void Torch_SessionLoaded()
        {
            MyEntities.OnEntityAdd += MotdOnce;
            Sync.Players.PlayerCharacterDied += ResetMotdOnce;
        }

        private void ResetMotdOnce(long obj)
        {
            _motdOnce.Remove(obj);
        }

        private void MotdOnce(MyEntity obj)
        {
            if (obj is MyCharacter character)
            {
                var id = character.ControllerInfo?.ControllingIdentityId ?? 0;
                if (string.IsNullOrEmpty(Config.Motd) || _motdOnce.Contains(id))
                    return;

                if (MySession.Static.Players.TryGetPlayerId(id, out MyPlayer.PlayerId info))
                    Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()
                        .SendMessageAsOther("MOTD", Config.Motd, MyFontEnum.Blue, info.SteamId);
                _motdOnce.Add(id);
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            Torch.SessionLoaded -= Torch_SessionLoaded;
            MyEntities.OnEntityAdd -= MotdOnce;
            Sync.Players.PlayerCharacterDied -= ResetMotdOnce;
            _config.Save();
        }
    }
}
