using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace adiIRC_DeepL_plugin_test
{
    internal class TestDriver
    {
        /// <summary>
        /// Driver for compiling and executing this plugin as a standalone application for test purposes.
        /// To use this, change compilation from Library to Console Application
        /// TODO:
        ///     Implement unit tests
        ///     Implement DeepL API Access
        /// </summary>
        /// <param name="args"></param>
        public static async Task Main(string[] args)
        {
            adiIRC_DeepL_plugin testPlugin = new adiIRC_DeepL_plugin();
            IChannel fuelratsChan = new IChannel();
            fuelratsChan.Name = "#fuelrats";
            IPluginHost pluginHost = new IPluginHost(fuelratsChan, ".\\");
            testPlugin.Initialize(pluginHost);

            //If API key is not saved, prompt user for key
            if(testPlugin.config_items.apikey == "") {
                Console.WriteLine("Input DeepL API Key: ");
                testPlugin.config_items.apikey = Console.ReadLine();
                testPlugin.save_config_items();
            }

            // Test deepl-any call
            // TODO: error handle API functions, and make unit tests
            await testPlugin.deepl_any(new RegisteredCommandArgs("_ FR This is a test.", fuelratsChan));
            //await testPlugin.deepl_any(new RegisteredCommandArgs("_ ZZ This is a test.", fuelratsChan));

            // Test basic EN ratsignal
            string rsig = "RATSIGNAL Case #3 PC HOR – CMDR IBZZ – System: \"TASCHETER SECTOR QN - T A3 - 1\" (Brown dwarf 79.9 LY from Sol) – Language: English (United Kingdom) (en-GB) (HOR_SIGNAL)";
            // Create example mecha rsig message
            ChannelNormalMessageArgs ratsignal = new ChannelNormalMessageArgs(rsig, fuelratsChan);
            ratsignal.User.Nick = "MechaSqueak[BOT]";
            testPlugin.deepl_auto_case(new RegisteredCommandArgs("", fuelratsChan));
            testPlugin.OnChannelNormalMessage(ratsignal);

            // Check if case was onboarded into correct monitor_items slot
            if (testPlugin.monitor_items[3] != null && 
                testPlugin.monitor_items[3].nickname.Equals("IBZZ")) Console.WriteLine("EN Rsig Autodetect Passed");
            else Console.WriteLine("EN Rsig Autodetect Failed");



            // Test RU ratsignal
            rsig = "RATSIGNAL Case #5 PC HOR – CMDR zzammaxx – System: \"SECTOR RU-C A14 - 2\" (Unconfirmed) – Language: Russian (Russia) (ru-RU) (HOR_SIGNAL)";
            ratsignal = new ChannelNormalMessageArgs(rsig, fuelratsChan);
            ratsignal.User.Nick = "MechaSqueak[BOT]";
            testPlugin.deepl_auto_case(new RegisteredCommandArgs("", fuelratsChan));
            testPlugin.OnChannelNormalMessage(ratsignal);

            // Check if case was onboarded into correct monitor_items slot
            if (testPlugin.monitor_items[5] != null && 
                testPlugin.monitor_items[5].nickname.Equals("zzammaxx") &&
                testPlugin.monitor_items[5].langcode.Equals("RU")) Console.WriteLine("RU Rsig Autodetect Passed");
            else Console.WriteLine("RU Rsig Autodetect Failed");


            // Test case remove
            testPlugin.deepl_rm(new RegisteredCommandArgs("3", fuelratsChan));
            // test case 3 should be null, and test case 5 should be unaffected
            if (testPlugin.monitor_items[3] == null && testPlugin.monitor_items[5].nickname.Equals("zzammaxx")) Console.WriteLine("Case Remove Passed");
            else Console.WriteLine("Case Remove Failed");


            // Test manual monitoring
            testPlugin.deepl_mon(new RegisteredCommandArgs("JaneDoe", fuelratsChan));
            if (testPlugin.monitor_items[20] != null &&
                testPlugin.monitor_items[20].nickname.Equals("JaneDoe") &&
                testPlugin.monitor_items[20].langcode.Equals("ZZ")) Console.WriteLine("Manual Monitor Add Passed");
            else Console.WriteLine("Manual Monitor Add Failed");


            // Test manual remove
            testPlugin.deepl_rm(new RegisteredCommandArgs("JaneDoe", fuelratsChan));
            if (testPlugin.monitor_items.Count == 20) Console.WriteLine("Manual Monitor Remove Passed");
            else Console.WriteLine("Manual Monitor Remove Failed");


            // Print help message
            //testPlugin.deepl_help(new RegisteredCommandArgs("", fuelratsChan));

            Console.Write("Press the any key to continue...");
            Console.Read();

        }
    }
}
