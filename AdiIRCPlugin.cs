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
        public string nickname, cmdr, platform, langcode;  // The nickname to monitor
        public IWindow window;   // The window to output translated messages into.

        public monitorItem(string nickname, string cmdr, IWindow window, string langcode = "EN", string platform = "")
        {
            //A case object, keeping track of a client's nick, cmdr, platform, and channel window
            this.nickname = nickname;
            this.cmdr = cmdr;
            this.langcode = langcode;
            this.platform = platform; //for future use, maybe
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
        private static bool drillmode = false;
        
        /// <summary>
        /// Writes changes to deepl.conf stored in %appdatalocal%\AdiIRC
        /// </summary>
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

        /// <summary>
        /// Reads JSON config file from %appdatalocal%\AdiIRC
        /// </summary>
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

        /// <summary>
        /// DeepL's API is used to translate text. A free account and API key are needed to use this integration.
        /// </summary>
        /// <param name="argument">Deepl API Key</param>
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

        /// <summary>
        /// Translates any language to any other language
        /// </summary>
        /// <param name="lang">Target Language</param>
        /// <param name="totranslate">Message to translate</param>
        /// <returns></returns>
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

        /// <summary>
        /// Calls deepl_translate_any to translate a client's message and print it in the same window
        /// </summary>
        /// <param name="lang">Rat's language, usually EN</param>
        /// <param name="totranslate">Message to translate</param>
        /// <param name="window">Window to post message</param>
        /// <param name="fromNick">Client's nick</param>
        private async void deepl_translate_towindow(string lang, string totranslate, IWindow window, string fromNick)
        {
            window.OutputText(fromNick + ": " + await deepl_translate_any(lang, totranslate));
        }

        /// <summary>
        /// Translates any language into English
        /// </summary>
        /// <param name="argument">Message to translate</param>
        private void deepl_en(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            deepl_translate_towindow("EN", allarguments, argument.Window, "yourself");
        }

        /// <summary>
        /// Helper function to parse arguments for deepl_translate_any()
        /// </summary>
        /// <param name="argument">language code, and message to translate</param>
        private async void deepl_any(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            string lang = allarguments.Substring(0, 2).ToUpper();
            string totranslate = allarguments.Substring(3);
            argument.Window.Editbox.Text = await deepl_translate_any(lang, totranslate);
        }

        /// <summary>
        /// Adds a user nick to translation monitor list
        /// </summary>
        /// <param name="argument">Nickname to monitor</param>
        private void deepl_mon(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            monitorItem monitorCandidate = new monitorItem(allarguments, allarguments, argument.Window, langcode: "ZZ");

            if (!IsNickMonitored(allarguments))
                monitor_items.Add(monitorCandidate); //add new entry into 20+ zone (ideally non-cases)
        }

        /// <summary>
        /// Removes a user nick from the monitor list.
        /// If the user is apart of an active case, it will blank out the case
        /// </summary>
        /// <param name="argument">Nick to remove from monitoring</param>
        private void deepl_rm(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);

            int index;
            if (int.TryParse(allarguments, out index))
            {
                if (index < monitor_items.Count)
                {
                    monitor_items[index] = null;
                }
                else
                    adihost.ActiveIWindow.OutputText("Could not find case #" + allarguments + " in monitor list.");
            }
            else if (IsNickMonitored(allarguments, out index)) {
                //remove a case
                if (index < 20) //0-19 index range reserved for cases, just null out
                {
                    monitor_items[index] = null;
                }
                //remove a nick
                else
                {
                    monitor_items.RemoveAt(index);
                }
            }
            else {
                adihost.ActiveIWindow.OutputText("Could not find Nick \"" + allarguments + "\" in monitor list.");
            }
        }

        /// <summary>
        /// Adds current channel into list of channels for monitoring
        /// </summary>
        /// <param name="argument">Current Channel</param>
        private void deepl_auto_case(RegisteredCommandArgs argument)
        {
            if (!channel_monitor_items.Contains(argument.Window))
            {
                channel_monitor_items.Add(argument.Window);
            }
        }

        /// <summary>
        /// Clears all monitoring
        /// </summary>
        /// <param name="argument">no args</param>
        private void deepl_clearmon(RegisteredCommandArgs argument)
        {
            monitor_items = new List<monitorItem>();
            for (int i = 0; i < 10; i++)
                monitor_items.Add(null);

            channel_monitor_items.Clear();
        }

        /// <summary>
        /// Excludes certain languages from being auto translated
        /// Useful if the rat speaks multiple languages
        /// </summary>
        /// <param name="argument">no args</param>
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

        /// <summary>
        /// Sets certain parameters in configuration
        /// keepNicks - Keep monitoring for nicks even if target disconnects
        /// removeNicks - Stop monitoring for nicks when they disconnect
        /// drillmode - toggles between listening for MechaSqueak or DrillSqueak
        /// </summary>
        /// <param name="argument">keepNicks/removeNicks/drillmode</param>
        private void deepl_set(RegisteredCommandArgs argument)
        {

            //TODO: change this setting to a toggle
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            if (allarguments.Equals("keepNicks"))
            {
                config_items.removePartingNicknames = false;
            }
            if (allarguments.Equals("removeNicks"))
            {
                config_items.removePartingNicknames = true;
            }

            if (allarguments.Equals("drillmode"))
            {
                // this config should only be in memory, not saved to deepl.conf
                drillmode = !drillmode;

                // print drillmode state after switch
                if (drillmode) adihost.ActiveIWindow.OutputText("DrillMode™ Enabled!");
                else adihost.ActiveIWindow.OutputText("DrillMode™ Disabled.");
            }
            save_config_items();
        }

        /// <summary>
        /// Prints list of monitored nicks, and channels
        /// </summary>
        /// <param name="argument">no args</param>
        private void deepl_debug(RegisteredCommandArgs argument)
        {
            int index = 0;
            foreach (monitorItem item in monitor_items)
            {
                if (item != null) adihost.ActiveIWindow.OutputText(String.Format("#{0} - Nick: {1}, Cmdr: {2}, Channel: {3}", index, item.nickname, item.cmdr, item.window.Name));
                index++;
            }
            foreach (IWindow window in channel_monitor_items)
            {
                adihost.ActiveIWindow.OutputText("Monitored Channel: " + window.Name);
            }
            adihost.ActiveIWindow.OutputText("Drillmode: " + drillmode);
        }

        private void deepl_help(RegisteredCommandArgs argument)
        {
            adihost.ActiveIWindow.OutputText("AdiIRC Deepl Plugin Command Reference");
            adihost.ActiveIWindow.OutputText("/deepl-api <api-key> - Sets your DeepL Api key. https://www.deepl.com/en/signup/?cta=checkout");
            adihost.ActiveIWindow.OutputText("/deepl-en <text> - Translates text to english");
            adihost.ActiveIWindow.OutputText("/deepl-any <langcode> <text> - Translates text to target language and places translation into active editbox");
            adihost.ActiveIWindow.OutputText("/deepl-mon <nickname> - Translates every message made by <nickname> to english");
            adihost.ActiveIWindow.OutputText("/deepl-rm <nickname>|<caseNumber> - Removes a single nickname or case number from the monitor list.");
            adihost.ActiveIWindow.OutputText("/deepl-auto-case - Identifies nicknames from new cases in active channel and add them to the monitor list");
            adihost.ActiveIWindow.OutputText("/deepl-clearmon - Clears the list of nicks to monitor for translations. Also disables case monitoring.");
            adihost.ActiveIWindow.OutputText("/deepl-exclude <langcode> - Adds a language code to the list of languages not to translate in auto-case mode.");
            adihost.ActiveIWindow.OutputText("/deepl-set removeNicks|keepNicks|drillmode - Configures certain behavious of the plugin. removeNicks -> autoremove monitored nicks that part the channel");
            adihost.ActiveIWindow.OutputText("/deepl-debug - Lists items monitored and/or other plugin debug information");
            adihost.ActiveIWindow.OutputText("/deepl-help - Shows this command reference");
        }

        /// <summary>
        /// Whenever a message comes in, this function will check if it's from Mecha/DrillSqueak
        /// or it will check if the message is from a monitored user. It will either create a new
        /// case entry if Mecha/Drill is posting a Ratsignal, or it will translate the monitored
        /// user's message.
        /// </summary>
        /// <param name="message"></param>
        private void OnChannelNormalMessage(ChannelNormalMessageArgs message)
        {
            IChannel channel = message.Channel;

            // If Mecha or DrillSqueak say anyting
            string botName = "MechaSqueak[BOT]";
            if (drillmode) botName = "DrillSqueak[BOT]";
            //channel.OutputText(message.User.Nick);
            //channel.OutputText(botName);
            if (message.User.Nick.Equals(botName))
            {
                // If channel is being monitored
                //channel.OutputText("Matched Bot");
                if (channel_monitor_items.Contains(channel))
                {
                    //channel.OutputText("Matched Channel");
                    // Identify Ratsignal or Drillsignal
                    string stripped = Regex.Replace(message.Message, @"(\x03(?:\d{1,2}(?:,\d{1,2})?)?)|\x02|\x0F|\x16|\x1F", "");
                    Regex regex = new Regex(@"(RAT|DRILL)SIGNAL Case #(?<caseNum>\d+) (PC)? ?(?<platform>ODY|HOR|LEG|Playstation|Xbox).*CMDR (?<cmdr>.+) – System: .* Language: .+ \((?<langcode>[a-z]{2})(?:-\w{2,3})?\)(?: – Nick: (?<nickname>[\w\[\]\^-{|}]+))?.?(?:\((?:ODY|HOR|LEG|XB|PS)_SIGNAL\))?");
                    Match match = regex.Match(stripped);
                    if (match.Success)
                    {
                        // parse various regex groups into new monitorItem
                        //channel.OutputText("Matched Ratsig");
                        if (match.Groups["langcode"].Success && match.Groups["caseNum"].Success)
                        {
                            string langcode = match.Groups["langcode"].Value.ToUpper();

                            //channel.OutputText("Langcode success: " + langcode);
                            int caseNum;
                            if (int.TryParse(match.Groups["caseNum"].Value, out caseNum))
                            {
                                // cmdr name should always match, nick is only present if different from cmdr name.
                                string cmdr = match.Groups["cmdr"].Value;

                                //channel.OutputText("Case number: " + caseNum);
                                //channel.OutputText("Cmdr: " + cmdr);

                                // if Fuel Rats is absurdly busy
                                if (caseNum >= monitor_items.Count)
                                {
                                    //make sure there is space for new case
                                    monitor_items.Add(null);
                                }
                                if (match.Groups["nickname"].Success)
                                {
                                    string nick = match.Groups["nickname"].Value;
                                    monitor_items[caseNum] = new monitorItem(nick, cmdr, channel, langcode: langcode);
                                }
                                else
                                {
                                    monitor_items[caseNum] = new monitorItem(cmdr, cmdr, channel, langcode: langcode);
                                }
                            }
                        }

                    }
                }
            }
            else
            {
                // else check if message is from a monitored user
                int index;
                if (IsNickMonitored(message.User.Nick, out index))
                {
                    // check if case lang is EN or on the exclude list
                    string langcode = monitor_items[index].langcode;
                    if (!langcode.Equals("EN") && !config_items.lang_no_translation.Contains(langcode))
                        deepl_translate_towindow("EN", message.Message, channel, message.User.Nick);
                }
            }
        }

        /// <summary>
        /// Checks to see if a given nick is being monitored
        /// 
        /// Return: Bool true if monitored, false if not
        /// Out int: If true, index will be where in the list the target is
        /// </summary>
        /// <param name="nickToFind">Which nick to search for</param>
        /// <param name="index">Out: index of located nick</param>
        /// <returns></returns>
        private bool IsNickMonitored(string nickToFind, out int index)
        {
            index = 0;
            foreach (monitorItem item in monitor_items)
            {
                if (item != null)
                    if (nickToFind.Equals(item.nickname) || nickToFind.Equals(item.cmdr))
                    {
                        return true;
                    }
                index++;
            }
            index = -1;
            return false;
        }

        /// <summary>
        /// Checks to see if a given nick is being monitored
        /// 
        /// Return: Bool true if monitored, false if not
        /// </summary>
        /// <param name="nickToFind"></param>
        /// <returns></returns>
        private bool IsNickMonitored(string nickToFind)
        {
            foreach (monitorItem item in monitor_items)
            {
                if (item != null)
                    if (nickToFind.Equals(item.nickname) || nickToFind.Equals(item.cmdr))
                    {
                        return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// When a user changes their Nick, check if the monitoring needs to be updated
        /// </summary>
        /// <param name="nickArgs"></param>
        private void OnNick(NickArgs nickArgs)
        {
            int index;
            if (IsNickMonitored(nickArgs.User.Nick, out index))
                monitor_items[index].nickname = nickArgs.NewNick;
        }

        /// <summary>
        /// When a user /quits, check if we need to stop monitoring
        /// Only autoremoves manually added nicks (use /deepl-rm to force remove a case)
        /// Cases will get naturally overwritten as new cases with the same casenumber come in
        /// </summary>
        /// <param name="quitArgs">Nick to check</param>
        private void OnQuit(QuitArgs quitArgs)
        {
            if (config_items.removePartingNicknames)
            {
                int index;
                if (IsNickMonitored(quitArgs.User.Nick, out index))
                    if (index > 19)
                        monitor_items.RemoveAt(index);
            }
        }
        /// <summary>
        /// When a user /parts, check if we need to stop monitoring
        /// Only autoremoves manually added nicks(use /deepl-rm to force remove a case)
        /// Cases will get naturally overwritten as new cases with the same casenumber come in
        /// </summary>
        /// <param name="partArgs">Nick to check</param>
        private void OnChannelPart(ChannelPartArgs partArgs) 
        {
            if (config_items.removePartingNicknames)
            {
                int index;
                if (IsNickMonitored(partArgs.User.Nick, out index))
                    if (index > 19)
                        monitor_items.RemoveAt(index);
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
            for (int i = 0; i < 20; i++)
                monitor_items.Add(null);
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
