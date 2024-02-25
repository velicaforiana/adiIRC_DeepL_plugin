namespace adiIRC_DeepL_plugin
{
    using AdiIRCAPIv2.Interfaces;
    using AdiIRCAPIv2.Arguments.Aliasing;
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    using AdiIRCAPIv2.Arguments.Contextless;
    using AdiIRCAPIv2.Arguments.Channel;
    using AdiIRCAPIv2.Arguments.ChannelMessages;

    public class deepl_json_response
    {
        public List<deepl_translation> translations;
    }

    public class deepl_translation
    {
        public string detected_source_language;
        public string text;
    }

    public class monitorItem
    {
        public string nickname;  // The nickname to monitor
        public IWindow window;   // The window to output translated messages into.

        public monitorItem(string nickname, IWindow window)
        {
            this.nickname = nickname;
            this.window = window;
        }
    }
    public class deepl_config_items
    {
        public string apikey;    // Api Key sent with all deepl calls
        public List<string> lang_no_translation;  // List of language codes skip when adding new nicks to monitoring
        public bool removePartingNicknames; // Whether or not to autoremove monitored nicknames that leave the channel.

        public deepl_config_items()
        {
            removePartingNicknames = false;
            apikey = "";
            lang_no_translation = new List<string>();
        }
    }

    public class adiIRC_DeepL_plugin : IPlugin
    {
        public string PluginName { get { return "adi_deepl"; } }

        public string PluginDescription { get { return "Supports translation commands using the DeepL API"; } }

        public string PluginAuthor { get { return "Velica Foriana"; } }

        public string PluginVersion { get { return "1.0"; } }

        public string PluginEmail { get { return "velicaforiana@******"; } }

        private IPluginHost adihost;
        private string deepl_config_file;
        private deepl_config_items config_items;
        private List<monitorItem> monitor_items;
        private List<IWindow> channel_monitor_items;

        private void save_config_items()
        {
            try
            {
                System.IO.File.WriteAllText(deepl_config_file, JsonConvert.SerializeObject(config_items));
                adihost.ActiveIWindow.OutputText("Saved Configs to: " + deepl_config_file);
            }
            catch (Exception e)
            {
                adihost.ActiveIWindow.OutputText(e.ToString());
            }
        }

        private void load_config_items()
        {
            if (System.IO.File.Exists(deepl_config_file))
            {
                try
                {
                    config_items = JsonConvert.DeserializeObject<deepl_config_items>(System.IO.File.ReadAllText(deepl_config_file));
                }
                catch (Exception e)
                {
                    adihost.ActiveIWindow.OutputText(e.ToString());
                }
            }
        }

        private void set_DeepL_ApiKey(RegisteredCommandArgs argument)
        {
            try
            {
                config_items.apikey = argument.Command.Split(' ')[1];
                save_config_items();
            }
            catch (Exception e)
            {
                adihost.ActiveIWindow.OutputText(e.ToString());
            }
            adihost.ActiveIWindow.OutputText("API Key set.");
        }

        private async Task<string> deepl_translate_any(string lang, string totranslate)
        {
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                HttpClient httpClient = new HttpClient(handler);
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api-free.deepl.com/v2/translate"))
                {
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    dict.Add("text", totranslate);
                    dict.Add("target_lang", lang);
                    requestMessage.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", config_items.apikey);
                    requestMessage.Content = new FormUrlEncodedContent(dict);

                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                    if (response.IsSuccessStatusCode)
                    {
                        deepl_json_response jsonResponse = JsonConvert.DeserializeObject<deepl_json_response>(await response.Content.ReadAsStringAsync());
                        return jsonResponse.translations[0].text;
                    }
                }
            }
            catch (Exception e)
            {
                adihost.ActiveIWindow.OutputText(e.ToString());
            }
            return "";
        }

        private async void deepl_translate_towindow(string lang, string totranslate, IWindow window, string fromNick)
        {
            window.OutputText(fromNick + ": " + await deepl_translate_any(lang, totranslate));
        }

        private void deepl_en(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            deepl_translate_towindow("EN", allarguments, argument.Window, "yourself");
        }

        private async void deepl_any(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            string lang = allarguments.Substring(0, 2).ToUpper();
            string totranslate = allarguments.Substring(3);
            argument.Window.Editbox.Text = await deepl_translate_any(lang, totranslate);
        }

        private void deepl_mon(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            monitorItem monitorCandidate = new monitorItem(allarguments, argument.Window);
            if (!monitor_items.Contains(monitorCandidate))
            {
                monitor_items.Add(monitorCandidate);
            }
        }
        private void deepl_rm(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            foreach (monitorItem item in monitor_items)
            {
                if (item.nickname.Equals(allarguments))
                {
                    monitor_items.Remove(item);
                    return;
                }
            }
        }
        private void deepl_auto_case(RegisteredCommandArgs argument)
        {
            if (!channel_monitor_items.Contains(argument.Window))
            {
                channel_monitor_items.Add(argument.Window);
            }
        }

        private void deepl_clearmon(RegisteredCommandArgs argument)
        {
            monitor_items.Clear();
            channel_monitor_items.Clear();
        }
        private void deepl_exclude(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            string langcode = allarguments.ToUpper();
            if (!config_items.lang_no_translation.Contains(langcode))
            {
                config_items.lang_no_translation.Add(langcode);
                save_config_items();
            }
        }
        private void deepl_set(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            if (allarguments.Equals("keepNicks"))
            {
                config_items.removePartingNicknames = false;
            }
            if (allarguments.Equals("removeNicks"))
            {
                config_items.removePartingNicknames = true;
            }
            save_config_items();
        }
        private void deepl_debug(RegisteredCommandArgs argument)
        {
            foreach (monitorItem item in monitor_items)
            {
                adihost.ActiveIWindow.OutputText("Monitored Nick: " + item.nickname);
            }
            foreach (IWindow window in channel_monitor_items)
            {
                adihost.ActiveIWindow.OutputText("Monitored Channel: " + window.Name);
            }
        }

        private void deepl_help(RegisteredCommandArgs argument)
        {
            adihost.ActiveIWindow.OutputText("AdiIRC Deepl Plugin Command Reference");
            adihost.ActiveIWindow.OutputText("/deepl-api <api-key> - Sets your DeepL Api key. https://www.deepl.com/en/signup/?cta=checkout");
            adihost.ActiveIWindow.OutputText("/deepl-en <text> - Translates text to english");
            adihost.ActiveIWindow.OutputText("/deepl-any <langcode> <text> - Translates text to target language and places translation into active editbox");
            adihost.ActiveIWindow.OutputText("/deepl-mon <nickname> - Translates every message made by <nickname> to english");
            adihost.ActiveIWindow.OutputText("/deepl-rm <nickname> - Removes a single nickname from the monitor list.");
            adihost.ActiveIWindow.OutputText("/deepl-auto-case - Identifies nicknames from new cases in active channel and add them to the monitor list");
            adihost.ActiveIWindow.OutputText("/deepl-clearmon - Clears the list of nicks to monitor for translations. Also disables case monitoring.");
            adihost.ActiveIWindow.OutputText("/deepl-exclude <langcode> - Adds a language code to the list of languages not to translate in auto-case mode.");
            adihost.ActiveIWindow.OutputText("/deepl-set removeNicks|keepNicks - Configures certain behavious of the plugin. removeNicks -> autoremove monitored nicks that part the channel");
            adihost.ActiveIWindow.OutputText("/deepl-debug - Lists items monitored and/or other plugin debug information");
            adihost.ActiveIWindow.OutputText("/deepl-help - Shows this command reference");
        }

        private void OnChannelNormalMessage(ChannelNormalMessageArgs message)
        {
            IChannel channel = message.Channel;
            if (message.User.Nick.Equals("MechaSqueak[BOT]"))
            {
                if (channel_monitor_items.Contains(channel))
                {
                    string stripped = Regex.Replace(message.Message, @"(\x03(?:\d{1,2}(?:,\d{1,2})?)?)|\x02|\x0F|\x16|\x1F", "");
                    Regex regex = new Regex(@"RATSIGNAL Case #.+ CMDR (?<cmdr>.+) – System: .* Language: .+ \((?<langcode>[a-z]{2})(?:-\w{2,3})?\)(?: – Nick: (?<nickname>[\w\[\]\^-{|}]+))?.?(?:\((?:ODY|HOR|LEG|XB|PS)_SIGNAL\))?");
                    Match match = regex.Match(stripped);
                    if (match.Success)
                    {
                        //channel.OutputText("Matched Ratsig");
                        if (match.Groups["langcode"].Success)
                        {
                            string langcode = match.Groups["langcode"].Value.ToUpper();
                            //channel.OutputText("Langcode success: " + langcode);
                            if (!langcode.Equals("EN") && !config_items.lang_no_translation.Contains(langcode))
                            {
                                if (match.Groups["nickname"].Success)
                                {
                                    monitor_items.Add(new monitorItem(match.Groups["nickname"].Value, channel));
                                }
                                else if (match.Groups["cmdr"].Success)
                                {
                                    monitor_items.Add(new monitorItem(match.Groups["cmdr"].Value, channel));
                                }
                            }
                        }
                        
                    }
                }
            }
            else
            {
                foreach (monitorItem item in monitor_items)
                {
                    if (message.User.Nick.Equals(item.nickname))
                    {
                        deepl_translate_towindow("EN", message.Message, channel, item.nickname);
                    }
                }
            }
        }

        private void OnNick(NickArgs nickArgs)
        {
            foreach (monitorItem item in monitor_items)
            {
                if (item.nickname == nickArgs.User.Nick)
                {
                    item.nickname = nickArgs.NewNick;
                    return;
                }
            }
        }

        private void OnQuit(QuitArgs quitArgs)
        {
            if (config_items.removePartingNicknames)
            {
                foreach (monitorItem item in monitor_items)
                {
                    if (item.nickname == quitArgs.User.Nick)
                    {
                        monitor_items.Remove(item);
                        return;
                    }
                }
            }
        }

        private void OnChannelPart(ChannelPartArgs partArgs) 
        {
            if (config_items.removePartingNicknames)
            {
                foreach (monitorItem item in monitor_items)
                {
                    if (item.nickname == partArgs.User.Nick)
                    {
                        monitor_items.Remove(item);
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            // Called when the plugin is unloaded/closed, do clean up here
        }

        public void Initialize(IPluginHost pluginHost)
        {
            adihost = pluginHost;
            string configdir = pluginHost.ConfigFolder;
            deepl_config_file = configdir + "deepl.conf";
            config_items = new deepl_config_items();
            monitor_items = new List<monitorItem>();
            channel_monitor_items = new List<IWindow>();
            load_config_items();
            adihost.HookCommand("/deepl-api", set_DeepL_ApiKey);
            adihost.HookCommand("/deepl-en", deepl_en);
            adihost.HookCommand("/deepl-any", deepl_any);
            adihost.HookCommand("/deepl-mon", deepl_mon);
            adihost.HookCommand("/deepl-rm", deepl_rm);
            adihost.HookCommand("/deepl-auto-case", deepl_auto_case);
            adihost.HookCommand("/deepl-clearmon", deepl_clearmon);
            adihost.HookCommand("/deepl-exclude", deepl_exclude);
            adihost.HookCommand("/deepl-set", deepl_set);
            adihost.HookCommand("/deepl-debug", deepl_debug);
            adihost.HookCommand("/deepl-help", deepl_help);
            adihost.OnChannelNormalMessage += OnChannelNormalMessage;
            adihost.OnNick += OnNick;
            adihost.OnQuit += OnQuit;
            adihost.OnChannelPart += OnChannelPart;
        }
    }
}
