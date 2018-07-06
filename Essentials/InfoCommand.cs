using System.Runtime.CompilerServices;

namespace Essentials
{
    public class InfoCommand
    {
        private string _command;
        private string _chatResponse;
        private string _dialogResponse;

        public string Command
        {
            get => _command;
            set
            {
                _command = value;
                NotifyPropertyChanged();
            }
        }

        public string ChatResponse
        {
            get => _chatResponse;
            set
            {
                _chatResponse = value;
                NotifyPropertyChanged();
            }
        }

        public string DialogResponse
        {
            get => _dialogResponse;
            set
            {
                _dialogResponse = value;
                NotifyPropertyChanged();
            }
        }

        private static void NotifyPropertyChanged([CallerMemberName] string propName = "")
        {
            EssentialsPlugin.Instance?.Config?.NotifyPropertyChanged(propName);
        }

        public override string ToString()
        {
            return Command;
        }
    }
}
