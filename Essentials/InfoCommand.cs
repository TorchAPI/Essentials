using System.Runtime.CompilerServices;
using Torch;

namespace Essentials
{
    public class InfoCommand : ViewModel
    {
        private string _command;
        private string _chatResponse;
        private string _dialogResponse;
        private string _urlResponse;

        public string Command
        {
            get => _command;
            set => SetValue(ref _command, value);
        }

        public string ChatResponse
        {
            get => _chatResponse;
            set =>SetValue(ref _chatResponse, value);
        }

        public string DialogResponse
        {
            get => _dialogResponse;
            set => SetValue(ref _dialogResponse, value);
        }

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
