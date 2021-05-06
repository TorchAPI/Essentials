using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProtoBuf;
using Sandbox;
using Sandbox.Engine.Networking;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage;
using VRage.Game;
using VRage.Serialization;
using Game = Sandbox.Engine.Platform.Game;

namespace Essentials.Patches
{
    public static class GameTagsPatch
    {
        [ReflectedMethodInfo(typeof(MyCachedServerItem), "SendSettingsToSteam")]
#pragma warning disable 649
        private static readonly MethodInfo _sendSettingsToSteamMethod;

        [ReflectedMethodInfo(typeof(GameTagsPatch), nameof(Prefix))]
        private static readonly MethodInfo _prefix;

        [ReflectedMethod(Name = "MemberwiseClone")]
        private static Func<object, object> _memberwiseClone;
#pragma warning restore 649
        
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(_sendSettingsToSteamMethod).Prefixes.Add(_prefix);
        }

        private static bool Prefix()
        {
            if (!Game.IsDedicated || MyGameService.GameServer == null) return true;
            byte[] array;
            using (var memoryStream = new MemoryStream())
            {
                var settings = (MyObjectBuilder_SessionSettings)_memberwiseClone(MySession.Static.Settings);
                settings.BlockTypeLimits = new SerializableDictionary<string, short>();
                
                var myServerData = new MyCachedServerItem.MyServerData
                {
                    Settings = settings,
                    ExperimentalMode = MySession.Static.IsSettingsExperimental(),
                    // to determinate "it's not a vanilla"
                    Mods = new List<WorkshopId> {new WorkshopId(1406994352, "Steam")},
                    Description = MySandboxGame.ConfigDedicated?.ServerDescription
                };
                
                Serializer.Serialize(memoryStream, myServerData);
                array = MyCompression.Compress(memoryStream.ToArray());
            }

            MyGameService.GameServer.SetKeyValue("sc", array.Length.ToString());

            for (var i = 0; i < Math.Ceiling(array.Length / 93.0); i++)
            {
                byte[] part;
                
                var partLength = array.Length - 93 * i;
                
                if (partLength >= 93)
                {
                    part = new byte[93];
                    Array.Copy(array, i * 93, part, 0, 93);
                }
                else
                {
                    part = new byte[partLength];
                    Array.Copy(array, i * 93, part, 0, partLength);
                }

                MyGameService.GameServer.SetKeyValue($"sc{i}", Convert.ToBase64String(part));
            }


            return false;
        }
    }
}