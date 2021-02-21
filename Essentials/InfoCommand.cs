using System.Runtime.CompilerServices;
using Torch;
using Torch.Views;

namespace Essentials
{
    public class InfoCommand : ViewModel
    {
        private string _command;
        private string _chatResponse;
        private string _dialogResponse;
        private string _urlResponse;

        [Display(Order = 1, Name = "Command", Description = "Type this in chat to activate command")]
        public string Command
        {
            get => _command;
            set => SetValue(ref _command, value);
        }

        [Display(Order = 2, Name = "Chat Response", Description = "Chat response to command")]
        public string ChatResponse
        {
            get => _chatResponse;
            set =>SetValue(ref _chatResponse, value);
        }

        [Display(Order = 3, Name = "Dialog Response", Description = "Dialog box response")]
        public string DialogResponse
        {
            get => _dialogResponse;
            set => SetValue(ref _dialogResponse, value);
        }

        [Display(Order = 4, Name = "URL", Description = "url response to command")]
        public string URL
        {
            get => _urlResponse;
            set => SetValue(ref _urlResponse, value);
        }
        
        public override string ToString()
        {
            return Command;
        }
    }
}
