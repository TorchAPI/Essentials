using System.Linq;
using Torch.Commands;
using Torch.Managers.ChatManager;
using Torch.API.Managers;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;

namespace Essentials.Commands
{
    [Category("info")]
    public class InfoModule:CommandModule
    {
        public static void Init()
        {
            var c = EssentialsPlugin.Instance.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>();
            c.MessageProcessing += MessageProcessing;
        }

        [Command("list", "Lists all available info commands.")]
        public void List()
        {
            Context.Respond(string.Join(", ", EssentialsPlugin.Instance.Config.InfoCommands.Select(i => i.Command).Where(c => !string.IsNullOrEmpty(c))));
        }

        private static void MessageProcessing(Torch.API.Managers.TorchChatMessage msg, ref bool consumed)
        {
            var infoCommands = EssentialsPlugin.Instance.Config.InfoCommands;
            if (infoCommands == null)
                return;
                
            var c = infoCommands.FirstOrDefault(i => i.Command?.Equals(msg.Message) == true);
            if (c == null)
                return;
            
            consumed = true;
            if (!string.IsNullOrEmpty(c.ChatResponse))
                EssentialsPlugin.Instance.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsOther("Server", c.ChatResponse, MyFontEnum.Blue, msg.AuthorSteamId.Value);
            if (!string.IsNullOrEmpty(c.DialogResponse))
                ModCommunication.SendMessageTo(new DialogMessage(c.Command, content: c.DialogResponse), msg.AuthorSteamId.Value);
        }
    }
}
