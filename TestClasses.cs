using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace adiIRC_DeepL_plugin_test
{
    public struct Editbox
    {
        public string text;
        public string Text
        {
            get { return text; }
            set
            {
                text = value;
                Console.WriteLine("Editbox: " + text);
            }
        }
    }


    public class IWindow
    {

        public Editbox Editbox;
        public string Name;
        public IWindow(string windowName)
        {
            Name = windowName;
        }

        public void OutputText(string message)
        {
            Console.Out.WriteLine("Window Output: " + message);
        }
    }

    public class IServer
    {
        public List<IChannel> Channels;
        public IServer(List<IChannel> channels)
        {
            Channels = channels;
        }
    }

    public class IChannel : IWindow
    {
        public static string windowName;

        public IChannel(): base(windowName)
        {
        }

    }

    public class IWindowHost
    {
        public IWindowHost()
        {

        }
    }

    public class IPluginHost
    {

        public IWindow ActiveIWindow;
        public string ConfigFolder;
        public List<IServer> GetServers;
        public IPluginHost(IWindow activeIWindow, string configFolder, List<IServer> getServers)
        {
            ActiveIWindow = activeIWindow;
            ConfigFolder = configFolder;
            GetServers = getServers;
        }
    }

    public class RegisteredCommandArgs
    {
        public string Command;
        public IWindow Window;
        public Editbox Editbox;
        public RegisteredCommandArgs(string command, IWindow window)
        {
            Command = command;
            Window = window;
        }
    }

    public struct User
    {
        string nick;
        public string Nick
        {
            get { return nick; }
            set
            {
                Console.WriteLine("User nick changed from " + Nick + " to " + value);
                nick = value;
            }
        }
    }
    public class ChannelNormalMessageArgs
    {
        public string Message;
        public IChannel Channel;
        public User User;
        public ChannelNormalMessageArgs(string message, IChannel channel)
        {
            Message = message;
            Channel = channel;
        }
    }

    public class NickArgs : ChannelNormalMessageArgs
    {
        public static string message;
        public static IChannel channel;
        public string NewNick;
        public NickArgs(string newNick) : base(message, channel)
        {
            NewNick = newNick;
        }
    }

    public class QuitArgs : ChannelNormalMessageArgs
    {
        public static string message;
        public static IChannel channel;
        public QuitArgs() : base(message, channel)
        { }
    }

    public class ChannelPartArgs : ChannelNormalMessageArgs
    {
        public static string message;
        public static IChannel channel;
        public ChannelPartArgs() : base(message, channel)
        { }
    }
}
