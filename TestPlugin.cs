/// This is a copy of the AdiIRCPlugin.cs class.
/// It should be nearly identical, except where 
/// public exposure is required to access functions 
/// for testing.
namespace adiIRC_DeepL_plugin_test
{
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    /*using AdiIRCAPIv2.Interfaces;
    using AdiIRCAPIv2.Arguments.Aliasing;
    using AdiIRCAPIv2.Arguments.Contextless;
    using AdiIRCAPIv2.Arguments.Channel;
    using AdiIRCAPIv2.Arguments.ChannelMessages;*/



    public class deepl_json_response
    {
        public List<deepl_translation> translations;
        public string message;
    }

    public class deepl_translation
    {
        public string detected_source_language;
        public string text;
        public deepl_translation()
        {
            detected_source_language = "";
            text = "";
        }
    }

    public class monitorItem
    {
        public string nickname, cmdr, platform, langcode, initLang;  // The nickname to monitor
        public int retries = 0;

        public monitorItem(string nickname, string cmdr, string langcode = "ZZ", string platform = "")
        {
            //A case object, keeping track of a client's nick, cmdr, platform, and channel window
            this.nickname = nickname;
            this.cmdr = cmdr;
            this.langcode = langcode;
            this.initLang = langcode;
            this.platform = platform; //for future use, maybe
        }
    }
    public class deepl_config_items
    {
        public string apikey, native_lang, api_endpoint;    // Api Key sent with all deepl calls
        public List<string> lang_no_translation;  // List of language codes skip when adding new nicks to monitoring
        public bool removePartingNicknames; // Whether or not to autoremove monitored nicknames that leave the channel.
        public List<string> channel_monitor_items;

        public deepl_config_items()
        {
            removePartingNicknames = false;
            apikey = "";
            native_lang = "EN";
            lang_no_translation = new List<string>();
            channel_monitor_items = new List<string>();
            api_endpoint = "api-free.deepl.com";
        }
    }

    public class adiIRC_DeepL_plugin
    {
        public string PluginName { get { return "adi_deepl"; } }

        public string PluginDescription { get { return "Supports translation commands using the DeepL API"; } }

        public string PluginAuthor { get { return "Velica Foriana"; } }

        public string PluginVersion { get { return "1.0"; } }

        public string PluginEmail { get { return "velicaforiana@******"; } }

        private IPluginHost adihost;
        private string deepl_config_file;
        public deepl_config_items config_items;
        public List<monitorItem> monitor_items;
        public static bool drillmode = false, debugmode = false, reverseTranslate = false;
        public const string NO_LANG = "ZZ";

        /// <summary>
        /// If debugmode = true, print the message to the active window
        /// </summary>
        /// <param name="message">Message to print</param>
        public void PrintDebug(string message)
        {
            if (debugmode) adihost.ActiveIWindow.OutputText("DEBUG: " + message);
        }

        /// <summary>
        /// If debugmode = true, print the message to the active window
        /// This overload will do string.Format()
        /// </summary>
        /// <param name="message">Formattable message to print</param>
        /// <param name="formatArgs">Strings to insert into formattable message</param>
        public void PrintDebug(string message, params string[] formatArgs)
        {
            if (debugmode) adihost.ActiveIWindow.OutputText("DEBUG: " + string.Format(message, formatArgs));
        }

        /// <summary>
        /// Writes changes to deepl.conf stored in %appdatalocal%\AdiIRC
        /// </summary>
        public void save_config_items()
        {
            try
            {
                System.IO.File.WriteAllText(deepl_config_file, JsonConvert.SerializeObject(config_items));
                PrintDebug("Saved Configs to: " + deepl_config_file);
            }
            catch (Exception e)
            {
                adihost.ActiveIWindow.OutputText(e.ToString());
            }
        }


        /// <summary>
        /// Reads JSON config file from %appdatalocal%\AdiIRC
        /// </summary>
        public void load_config_items()
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
        public void set_DeepL_ApiKey(RegisteredCommandArgs argument)
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
        /// <param name="sourceLang">(Optional) Tell DeepL source language for better short translations</param>
        /// <returns></returns>
        public async Task<deepl_translation> deepl_translate(string lang, string totranslate, string sourceLang="", bool shouldRetry=true)
        {
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                HttpClient httpClient = new HttpClient(handler);
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://" + config_items.api_endpoint + "/v2/translate"))
                {
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    dict.Add("text", totranslate);
                    dict.Add("target_lang", lang);
                    if (!string.IsNullOrEmpty(sourceLang)) dict.Add("source_lang", sourceLang);
                    requestMessage.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", config_items.apikey);
                    requestMessage.Content = new FormUrlEncodedContent(dict);

                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (responseContent.Contains("Wrong endpoint. Use https://api.deepl.com"))
                    {
                        config_items.api_endpoint = "api.deepl.com";
                        save_config_items();
                        if (shouldRetry)
                            return await deepl_translate(lang, totranslate, sourceLang, false);
                        else
                            return null;
                    }


                    deepl_json_response jsonResponse = JsonConvert.DeserializeObject<deepl_json_response>(responseContent);
                    try
                    {
                        jsonResponse = JsonConvert.DeserializeObject<deepl_json_response>(responseContent);
                        if (response.IsSuccessStatusCode)
                        {
                            // If the sourceLang was incorrect, toTranslate and the translation will be the same, re-run the translation with no langcode
                            if (jsonResponse.translations[0].text.Equals(totranslate))
                            {
                                // If this is the second attempt, or retrying is otherwise disabled, stop trying
                                if (!shouldRetry) return null;
                                return await deepl_translate(lang, totranslate, shouldRetry: false);
                            }
                            return jsonResponse.translations[0];
                        }
                        else
                        {
                            // Normal Adi errors such as 'bad lang code' will be communicated through the 'message' json parameter
                            if (!string.IsNullOrEmpty(jsonResponse.message))
                                adihost.ActiveIWindow.OutputText(jsonResponse.message);
                            return null;
                        }
                    }
                    catch (Exception e)
                    {
                        adihost.ActiveIWindow.OutputText("DeepL Plugin: Failed to parse DeepL API Response. See: " + adihost.ConfigFolder + "\\deepl_debug.log");
                        try
                        {
                            System.IO.File.WriteAllText(adihost.ConfigFolder + "deepl_debug.log", DateTime.Now.ToString() + String.Format(" - Translation text: {0} | Source Lang: {1} | Target Lang: {2}", totranslate, sourceLang, lang));
                            System.IO.File.WriteAllText(adihost.ConfigFolder + "deepl_debug.log", DateTime.Now.ToString() + " - " + responseContent);
                            System.IO.File.WriteAllText(adihost.ConfigFolder + "deepl_debug.log", DateTime.Now.ToString() + " - " + e.Message);
                        }
                        catch (Exception f)
                        {
                            adihost.ActiveIWindow.OutputText("DeepL Plugin: Failed to write to debug log.");
                            adihost.ActiveIWindow.OutputText(f.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                adihost.ActiveIWindow.OutputText("DeepL Plugin: Failed to send translation request. General error.");
                adihost.ActiveIWindow.OutputText(e.ToString());
            }
            return new deepl_translation();
        }


        /// <summary>
        /// Calls deepl_translate_any to translate a client's message and print it in the same window
        /// </summary>
        /// <param name="lang">Rat's language, usually EN</param>
        /// <param name="totranslate">Message to translate</param>
        /// <param name="window">Window to post message</param>
        /// <param name="fromUser">User's MonitorItem</param>
        public async Task<string> deepl_translate_towindow(string lang, string totranslate, IWindow window, monitorItem fromUser) //return string for test validation purposes
        {
            deepl_translation translation;
            //Try to use the detect langcode from the user
            if (!fromUser.langcode.Equals(NO_LANG)) translation = await deepl_translate(lang, totranslate, fromUser.langcode);
            else translation = await deepl_translate(lang, totranslate);
            
            //If the detected langcode was EN, or translation is empty (failed), start to disable auto-translation
            if (translation == null || translation.detected_source_language.Equals(config_items.native_lang) || (string.IsNullOrEmpty(translation.detected_source_language) && string.IsNullOrEmpty(translation.text)))
            {
                fromUser.retries++;
                PrintDebug("Translation failure. Increasing {0} retry count to {1}.", fromUser.nickname, fromUser.retries + "");
                if (fromUser.retries >= 3)
                {
                    fromUser.langcode = config_items.native_lang;
                    PrintDebug("Set user {0} to English.", fromUser.nickname);
                }
                return "";
            }
            //If the translation returns with a different langcode, update user's langcode
            else if (!translation.detected_source_language.Equals(fromUser.langcode))
            {
                PrintDebug("Changing {0}'s langcode to detected: {1}", fromUser.nickname, translation.detected_source_language);
                fromUser.langcode = translation.detected_source_language;
                fromUser.retries = 0;
            }
            else
                fromUser.retries = 0;
            window.OutputText(fromUser.nickname + "(" + translation.detected_source_language + "): " + translation.text);

            // TEST PURPOSES ONLY
            return fromUser.nickname + "(" + translation.detected_source_language + "): " + translation.text; // FOR TEST ONLY
        }
    

        /// <summary>
        /// Translates any language into English
        /// </summary>
        /// <param name="argument">Message to translate</param>
        public async Task<string> deepl_en(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            return await deepl_translate_towindow(config_items.native_lang, allarguments, argument.Window, new monitorItem("Yourself", "Yourself", NO_LANG));
        }

        /// <summary>
        /// Helper function to parse arguments for deepl_translate_any()
        /// </summary>
        /// <param name="argument">language code or case number or nick, and message to translate</param>
        public async Task<string> deepl_any(RegisteredCommandArgs argument) // for testing, return Task<string> so we can await the completion of this function and validate output
        {
            string[] allArgs = argument.Command.Split(new char[] {' '}, 3);
            string lang, cmdr = "";

            // if first arg is a number, find the case, use lang and cmdr from the case
            int index;
            if (IsMonitored(allArgs[1], out index))
            {
                lang = monitor_items[index].initLang;
                cmdr = monitor_items[index].nickname;
            }
            else
                lang = allArgs[1];

            string totranslate = allArgs[2];
            deepl_translation translation = await deepl_translate(lang, totranslate);

            if (translation != null) //if not translation failure
            {  

                string translationText = translation.text;
                if (!string.IsNullOrEmpty(cmdr))
                    translationText = cmdr + ", " + translationText;
                argument.Window.Editbox.Text = translationText;

                //do a reverse translation back into user's native language
                deepl_translation reverseTranslation = null;
                if (reverseTranslate)
                {
                    reverseTranslation = await deepl_translate(config_items.native_lang, translation.text, lang);
                    argument.Window.OutputText("Reverse Translation: " + reverseTranslation.text);
                }

                // TEST PURPOSES ONLY
                if (reverseTranslate && reverseTranslation != null)
                    return translation.text + "|" + reverseTranslation.text;

                // TEST PURPOSES ONLY
                return translationText;
            }
            return "";
        }

        /// <summary>
        /// Adds a user nick to translation monitor list
        /// </summary>
        /// <param name="argument">Nickname to monitor</param>
        public void deepl_mon(RegisteredCommandArgs argument)
        {
            string allarguments = argument.Command.Substring(argument.Command.IndexOf(" ") + 1);
            monitorItem monitorCandidate = new monitorItem(allarguments, allarguments, langcode: NO_LANG);

            if (!IsMonitored(allarguments))
                monitor_items.Add(monitorCandidate); //add new entry into 20+ zone (ideally non-cases)
        }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="nick|caseNum">Nick or Case Number to update</param>
        /// <param name="newLang">New language code</param>
        public void deepl_lang(RegisteredCommandArgs argument)
        {
            string[] allArgs = argument.Command.Split(new char[] { ' ' }, 3);
            if (allArgs.Length >= 3)
            {
                string target = allArgs[1];
                string newLang = allArgs[2];
                if (newLang.Length == 2) { 
                    int index;
                    if (IsMonitored(target, out index))
                    {
                        monitor_items[index].langcode = newLang;
                    }
                    else
                        adihost.ActiveIWindow.OutputText("Warning: Could not find '" + target + "' in monitor list.");
                }
                else
                    adihost.ActiveIWindow.OutputText("Warning: New language should be a 2-letter language code. Example: /dl-lang NickName RU");
            }
            else
                adihost.ActiveIWindow.OutputText("Warning: Not enough arguments supplied. Usage: /dl-lang <nick|caseNumber> <langCode>");

        }

            /// <summary>
            /// Removes a user nick from the monitor list.
            /// If the user is apart of an active case, it will blank out the case
            /// </summary>
            /// <param name="argument">Nick to remove from monitoring</param>
            public void deepl_rm(RegisteredCommandArgs argument)
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
            else if (IsMonitored(allarguments, out index))
            {
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
            else
            {
                adihost.ActiveIWindow.OutputText("Could not find Nick \"" + allarguments + "\" in monitor list.");
            }
        }

        /// <summary>
        /// Adds current channel into list of channels for monitoring
        /// </summary>
        /// <param name="argument">Current Channel</param>
        public void deepl_auto_case(RegisteredCommandArgs argument)
        {
            if (!config_items.channel_monitor_items.Contains(argument.Window.Name))
            {
                config_items.channel_monitor_items.Add(argument.Window.Name);
                save_config_items();
            }
        }

        /// <summary>
        /// Clears all monitoring
        /// </summary>
        /// <param name="argument">no args</param>
        public void deepl_clearmon(RegisteredCommandArgs argument)
        {
            monitor_items = new List<monitorItem>();
            for (int i = 0; i < 10; i++)
                monitor_items.Add(null);

            config_items.channel_monitor_items.Clear();
            save_config_items();
        }

        /// <summary>
        /// Sets certain parameters in configuration
        /// </summary>
        /// <param name="argument">keepNicks/removeNicks/drillmode</param>
        public void deepl_set(RegisteredCommandArgs argument)
        {
            string[] allarguments = argument.Command.Split(' ');
            if (allarguments[1].Equals("autoRemoveNicks"))
            {
                config_items.removePartingNicknames = !config_items.removePartingNicknames;
                // print drillmode state after switch
                if (config_items.removePartingNicknames) adihost.ActiveIWindow.OutputText("Noncase Autoremove Enabled.");
                else adihost.ActiveIWindow.OutputText("Noncase Autoremove Disabled.");

                save_config_items();
            }

            if (allarguments[1].Equals("reverseTranslate"))
            {
                reverseTranslate = !reverseTranslate;
                // print drillmode state after switch
                if (reverseTranslate) adihost.ActiveIWindow.OutputText("/dl-any will be reverse translated.");
                else adihost.ActiveIWindow.OutputText("Reverse Translation Disabled.");

                save_config_items();
            }

            if (allarguments[1].Equals("native"))
            {
                if (allarguments.Length > 2)
                {
                    string langcode = allarguments[2].ToUpper();
                    config_items.native_lang = langcode;
                    save_config_items();
                }
                else
                    adihost.ActiveIWindow.OutputText("Error: 'native' setting requires language code argument");
            }

            if (allarguments[1].Equals("exclude"))
            {
                if (allarguments.Length > 2)
                {
                    string langcode = allarguments[2].ToUpper();
                    if (!config_items.lang_no_translation.Contains(langcode))
                    {
                        config_items.lang_no_translation.Add(langcode);
                        adihost.ActiveIWindow.OutputText(String.Format("Langcode '{0}' added to exclude list.", langcode));
                    }
                    else
                    {
                        config_items.lang_no_translation.Remove(langcode);
                        adihost.ActiveIWindow.OutputText(String.Format("Langcode '{0}' removed from exclude list.", langcode));
                    }
                    save_config_items();
                }
                else
                    adihost.ActiveIWindow.OutputText("Error: 'exclude' setting requires language code argument");
            }

            if (allarguments[1].Equals("drillmode"))
            {
                // this config should only be in memory, not saved to deepl.conf
                drillmode = !drillmode;

                // print drillmode state after switch
                if (drillmode) adihost.ActiveIWindow.OutputText("DrillMode™ Enabled!");
                else adihost.ActiveIWindow.OutputText("DrillMode™ Disabled.");
            }

            if (allarguments[1].Equals("debugmode"))
            {
                // this config should only be in memory, not saved to deepl.conf
                debugmode = !debugmode;

                // print drillmode state after switch
                if (debugmode) adihost.ActiveIWindow.OutputText("DebugMode™ Enabled!");
                else adihost.ActiveIWindow.OutputText("DebugMode™ Disabled.");
            }
        }

        /// <summary>
        /// Prints list of monitored nicks, and channels
        /// </summary>
        /// <param name="argument">no args</param>
        public void deepl_debug(RegisteredCommandArgs argument)
        {
            int index = 0;
            foreach (monitorItem item in monitor_items)
            {
                if (item != null) adihost.ActiveIWindow.OutputText(String.Format("#{0} - Nick: {1}, Lang: {2}", index, item.nickname, item.langcode));
                index++;
            }

            string monitoredChannels = "";
            foreach (string channelName in config_items.channel_monitor_items)
            {
                monitoredChannels += channelName + ", ";
            }
            monitoredChannels = monitoredChannels.TrimEnd(' ', ',');

            string excludedLangs = "";
            foreach (string lang in config_items.lang_no_translation)
            {
                excludedLangs += lang + ", ";
            }
            excludedLangs = excludedLangs.TrimEnd(' ', ',');

            adihost.ActiveIWindow.OutputText("Native Language: " + config_items.native_lang);
            adihost.ActiveIWindow.OutputText("Monitored Channels: " + monitoredChannels);
            adihost.ActiveIWindow.OutputText("Excluded Languages: " + excludedLangs);
            adihost.ActiveIWindow.OutputText("AutoRemoveNick: " + config_items.removePartingNicknames);
            adihost.ActiveIWindow.OutputText("ReverseTranslate: " + reverseTranslate);
            adihost.ActiveIWindow.OutputText("Drillmode: " + drillmode);
            adihost.ActiveIWindow.OutputText("Debugmode: " + debugmode);
        }

        public void deepl_help(RegisteredCommandArgs argument)
        {
            adihost.ActiveIWindow.OutputText("AdiIRC Deepl Plugin Command Reference");
            adihost.ActiveIWindow.OutputText("/dl-api <api-key> - Sets your DeepL Api key. https://www.deepl.com/en/signup/?cta=checkout");
            adihost.ActiveIWindow.OutputText("/dl-en <text> - Translates text to english");
            adihost.ActiveIWindow.OutputText("/dl-any <langcode|caseNumber> <text> - Translates text to target language and places translation into active editbox");
            adihost.ActiveIWindow.OutputText("/dl-mon <nickname> - Translates every message made by <nickname> to your native language");
            adihost.ActiveIWindow.OutputText("/dl-lang <nickname|caseNumber> <langcode> - Changes the assumed language of a user or client");
            adihost.ActiveIWindow.OutputText("/dl-rm <nickname|caseNumber> - Removes a single nickname or case number from the monitor list");
            adihost.ActiveIWindow.OutputText("/dl-mecha - Starts monitoring for Fuel Rats cases announced by MechaSqueak in the active channel");
            adihost.ActiveIWindow.OutputText("/dl-clear - Clears the list of nicks to monitor for translations. Also disables case monitoring");
            adihost.ActiveIWindow.OutputText("/dl-set <option> - Configures certain behavious of the plugin");
            adihost.ActiveIWindow.OutputText("  exclude <langcode>  -> (config) add/remove language on list that should not be auto-translated");
            adihost.ActiveIWindow.OutputText("  native <langcode>   -> (config) change native langauge (default: EN)");
            adihost.ActiveIWindow.OutputText("  autoRemoveNicks     -> (config) toggles auto removal of non-case nicks on parts or quits");
            adihost.ActiveIWindow.OutputText("  reverseTranslate    -> (memory) toggles a reverse translation of /dl-any");
            adihost.ActiveIWindow.OutputText("  drillmode           -> (memory) toggles whether to observe MechaSqueak or DrillSqueak");
            adihost.ActiveIWindow.OutputText("  debugmode           -> (memory) toggles extra debug messages during operations");
            adihost.ActiveIWindow.OutputText("/dl-debug - Lists items monitored and other plugin debug information");
            adihost.ActiveIWindow.OutputText("/dl-help - Shows this command reference");
        }

        /// <summary>
        /// Whenever a message comes in, this function will check if it's from Mecha/DrillSqueak
        /// or it will check if the message is from a monitored user. It will either create a new
        /// case entry if Mecha/Drill is posting a Ratsignal, or it will translate the monitored
        /// user's message.
        /// </summary>
        /// <param name="message"></param>
        public string OnChannelNormalMessage(ChannelNormalMessageArgs message)
        {
            IChannel channel = message.Channel;

            // If Mecha or DrillSqueak say anyting
            string botName = "MechaSqueak[BOT]";
            if (drillmode) botName = "DrillSqueak[BOT]";
            if (message.User.Nick.Equals(botName))
            {
                // If channel is being monitored
                PrintDebug("Matched Bot");
                if (config_items.channel_monitor_items.Contains(channel.Name))
                {
                    // Identify Ratsignal or Drillsignal
                    PrintDebug("Matched Channel");
                    string stripped = Regex.Replace(message.Message, @"(\x03(?:\d{1,2}(?:,\d{1,2})?)?)|\x02|\x0F|\x16|\x1F", "");
                    Regex regex = new Regex(@"(RAT|DRILL)SIGNAL Case #(?<caseNum>\d+) (PC)? ?(?<platform>ODY|HOR|LEG|Playstation|Xbox).*CMDR (?<cmdr>.+?) (\(Offline\) )?– System: .* Language: .+ \((?<langcode>[a-z]{2})(?:-\w{2,3})?\)(?: – Nick: (?<nickname>[\w\[\]\^-{|}]+))?.?(?:\((?:ODY|HOR|LEG|XB|PS)_SIGNAL\))?");
                    Match match = regex.Match(stripped);
                    if (match.Success)
                    {
                        // parse various regex groups into new monitorItem
                        PrintDebug("Matched Ratsig");
                        if (match.Groups["langcode"].Success && match.Groups["caseNum"].Success)
                        {
                            string langcode = match.Groups["langcode"].Value.ToUpper();

                            PrintDebug("Langcode success: " + langcode);
                            int caseNum;
                            if (int.TryParse(match.Groups["caseNum"].Value, out caseNum))
                            {
                                // cmdr name should always match, nick is only present if different from cmdr name.
                                string cmdr = match.Groups["cmdr"].Value;

                                PrintDebug("Case number: " + caseNum);
                                PrintDebug("Cmdr: " + cmdr);

                                // if Fuel Rats is absurdly busy
                                if (caseNum >= monitor_items.Count)
                                {
                                    //make sure there is space for new case
                                    monitor_items.Add(null);
                                }
                                if (match.Groups["nickname"].Success)
                                {
                                    string nick = match.Groups["nickname"].Value;

                                    // check if repeat client
                                    int index;
                                    if (IsMonitored(nick, out index))
                                        monitor_items[index] = null;

                                    monitor_items[caseNum] = new monitorItem(nick, cmdr, langcode: langcode);
                                    PrintDebug("Monitoring " + nick);
                                }
                                else
                                {
                                    // check if repeat client
                                    int index;
                                    if (IsMonitored(cmdr, out index))
                                        monitor_items[index] = null;

                                    monitor_items[caseNum] = new monitorItem(cmdr, cmdr, langcode: langcode);
                                    PrintDebug("Monitoring " + cmdr);
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
                if (IsMonitored(message.User.Nick, out index))
                {
                    // check if case lang is EN or on the exclude list
                    string langcode = monitor_items[index].langcode;
                    if (!langcode.Equals(config_items.native_lang) && !config_items.lang_no_translation.Contains(langcode))
                    {
                        PrintDebug("Translating {0}'s message - {1}", message.User.Nick, message.Message);
                        return deepl_translate_towindow(config_items.native_lang, message.Message, channel, monitor_items[index]).Result;
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Checks to see if a given nick or case number is being monitored
        /// 
        /// Return: Bool true if monitored, false if not
        /// Out int: If true, index will be where in the list the target is
        /// </summary>
        /// <param name="toFind">Which nick or case number to search for</param>
        /// <param name="index">Out: index of located nick</param>
        /// <returns></returns>
        private bool IsMonitored(string toFind, out int index)
        {
            if (int.TryParse(toFind, out index)){
                if (index < monitor_items.Count && monitor_items[index] != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            index = 0;
            foreach (monitorItem item in monitor_items)
            {
                if (item != null)
                    if (toFind.Equals(item.nickname) || toFind.Equals(item.cmdr))
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
        private bool IsMonitored(string toFind)
        {
            int index;
            if (int.TryParse(toFind, out index))
            {
                if (monitor_items.Count < index && monitor_items[index] != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            foreach (monitorItem item in monitor_items)
            {
                if (item != null)
                    if (toFind.Equals(item.nickname) || toFind.Equals(item.cmdr))
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
        public void OnNick(NickArgs nickArgs)
        {
            int index;
            if (IsMonitored(nickArgs.User.Nick, out index))
                monitor_items[index].nickname = nickArgs.NewNick;
        }

        /// <summary>
        /// When a user /quits, check if we need to stop monitoring
        /// Only autoremoves manually added nicks (use /deepl-rm to force remove a case)
        /// Cases will get naturally overwritten as new cases with the same casenumber come in
        /// </summary>
        /// <param name="quitArgs">Nick to check</param>
        public void OnQuit(QuitArgs quitArgs)
        {
            if (config_items.removePartingNicknames)
            {
                int index;
                if (IsMonitored(quitArgs.User.Nick, out index))
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
        public void OnChannelPart(ChannelPartArgs partArgs) 
        {
            if (config_items.removePartingNicknames)
            {
                int index;
                if (IsMonitored(partArgs.User.Nick, out index))
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
            
            load_config_items();
            /*adihost.HookCommand("/dl-api", set_DeepL_ApiKey);
            adihost.HookCommand("/dl-en", deepl_en);
            adihost.HookCommand("/dl-any", deepl_any);
            adihost.HookCommand("/dl-mon", deepl_mon);
            adihost.HookCommand("/dl-lang", deepl_lang);
            adihost.HookCommand("/dl-rm", deepl_rm);
            adihost.HookCommand("/dl-mecha", deepl_auto_case);
            adihost.HookCommand("/dl-clear", deepl_clearmon);
            adihost.HookCommand("/dl-exclude", deepl_exclude);
            adihost.HookCommand("/dl-set", deepl_set);
            adihost.HookCommand("/dl-debug", deepl_debug);
            adihost.HookCommand("/dl-help", deepl_help);
            adihost.OnChannelNormalMessage += OnChannelNormalMessage;
            adihost.OnNick += OnNick;
            adihost.OnQuit += OnQuit;
            adihost.OnChannelPart += OnChannelPart;*/
        }
    }
}
