# AdiIRC Deepl Plugin

This plugin will monitor IRC nicknames for automatic translation to the user's native language using the DeepL API.

In addition to the general translation feature there are some [Fuel Rats](https://fuelrats.com) integrations as well.
Incoming cases and their language can be automatically identified and marked for tranlation if needed.

# Warnings

- Fuel Rats: This plugin is meant for experienced rats.
- Fuel Rat Dispatchers: Always ask the client if they speak English. It is always better to dispatch cases in English if the client speaks it fluently.

# Installation

### Prerequisites

 - [AdiIRC v4.4](https://adiirc.com/)
 - [.NET 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework)
 - [DeepL Free Account](https://www.deepl.com/en/signup/?cta=checkout)
 - [DeepL API Key](https://www.deepl.com/your-account/summary)

## Installation

 1. [Download](https://github.com/velicaforiana/adiIRC_DeepL_plugin/tags) the latest package
 2. Close AdiIRC
 3. Extract the 2 DLL files to your AdiIRC Plugin Directory
    - Default location: %localappdata%\AdiIRC\Plugins
 4. Open AdiIRC
 5. Click to Files -> Plugins.
    - If the plugin is not listed: Click "Install New". Select the "adiIRC_DeepL.dll" from the Plugins directory.
    - If the plugin is listed: Select "adiIRC_DeepL.dll" and click "Load"
 6. Type "/dl-api <API_Key>" in an AdiIRC editbox. (example: /dl-api 23f8******1:fx )
 7. Type "/dl-help"  to see if the help page prints.
   
# Usage

--  
**/dl-help**
```
/dl-help
Args:
 - None
Example usage:
/dl-help
```

Prints man page.

--  
**/dl-api**
```
/dl-api <api-key>
Args:
 - api-key: The API Key provided with a free DeepL account
Example usage:
/dl-api 45f3d41b-5b37-4a2c-404f-65424d7fddb1:fx
```

This plugin uses the DeepL API to perform its translations. Users must sign up for a free DeepL account [here](https://www.deepl.com/en/signup/?cta=checkout). Once the account is created, the API key can be retrieved [here](https://www.deepl.com/your-account/summary).

--  
**/dl-en**
```
/dl-en <message>
Args:
 - message: Non-english message that will be (hopefully) translated into the user's native language.
Example usage:
/dl-en Il s'agit d'un test.
```

This will translate a message from any DeepL supported language to user's native language, and print the resulting translation to the output window.

--  
**/dl-any**
```
/dl-any <langcode|caseNumber> <message>
Args:
 - langcode: Two letter language code for target translation
 - caseNumber: Integer that represents a FuelRats case
 - message: Message to translate into target language
Example usage:
/dl-any FR This is a test
/dl-any 3 This is a test
```

Translates any language into a target language. Usually this will be your native language into a non-native language. "/dl-set reverseTranslate" can be used to enable this function to translate the resulting message back into user's native language for inspection purposes.

When using a case number, the plugin will find the client's language code, and also prepend the client's name to the resulting message.

--  
**/dl-mon**
```
/dl-mon <nickname>
Args:
 - nickname: User nick you want to automatically translate
Example usage:
/dl-mon Delryn
```

Enables monitoring of a specific user, and will automatically translate any message that the user sends. If the translation is detected as English three times in a row, monitoring will be disabled to conserve API usage.

--  
**/dl-lang**
```
/dl-lang <nickname|caseNumber> <langcode>
Args:
 - nickname: User nick to change langcode for
 - caseNumber: Case number to change langcode for
 - langcode: New 2-letter langcode for monitoring (ISO 639 [set 1])
Example usage:
/dl-lang Delryn RU
/dl-lang 3 de
```

Changes what language you expect to see from a target nick or case. This is most useful for FuelRats dispatchers, but will make general translating more accurate if the assumed language matches the target's actual language.

--  
**/dl-rm**
```
/dl-rm <nickname|caseNumber>
Args:
 - nickname: User nick to stop monitoring
 - caseNumber: (For Fuel Rats) case number to stop monitoring
Example usage:
/dl-rm Delryn
/dl-rm 4
```

Clears monitoring of a specific user. For Fuel Rats usage, you can supply a case number to stop monitoring on a specific case.

--  
**/dl-mecha**
```
/dl-mecha
Args:
 - None
Example usage:
/dl-mecha
```

For Fuel Rat usage. Enables monitoring for Rat Signals from MechaSqueak[BOT] in the channel in which the command was executed. If a Rat Signal is detected, the plugin will parse the signal, and automatically start monitoring the client if their language is not English, or ignored.

--  
**/dl-clear**
```
/dl-clear
Args:
 - None
Example usage:
/dl-clear
```

Clears all currently monitored users and channels.

--  
**/dl-set**
```
/dl-rm <option>
Options:
  exclude <langcode>  -> (config) add language to list that should not be auto-translated
  native <langcode>   -> (config) change native langauge (default: EN)
  autoRemoveNicks  -> (config) toggles auto removal of non-case nicks when nick parts or quits
  reverseTranslate -> (memory) toggles a reverse translation of /dl-any
  drillmode        -> (memory) toggles whether to observe MechaSqueak or DrillSqueak
  debugmode        -> (memory) toggles extra debug messages during operations
Example usage:
/dl-set native DE
/dl-set autoRemoveNicks
```

Use this command to toggle various options. Options labeled as "(config)" will be written to the deepl.conf file and remembered between Adi client. Options labeled as "(memory)" will be forgotten between Adi client restarts.

- exclude <langcode> (config): For multi-lingual users, this will disable auto-translation for additional languages. Run this again to remove a language from the exclude list.
- native <langcode> (config): Change native language from default English to another language.
- autoRemoveNicks (config): When a monitored client leaves IRC, this will automatically remove them from monitoring. This does not apply to Fuel Rat case clients, monitored by /dl-mecha.
- reverseTranslate (memory): When using /dl-any, this will additionally take the resulting translation, and feed it back to DeepL to translate the message back into English. The reverse translated English message will be printed to the Output Window. This can be useful when trying to communicate nuanced information, and helps the user check if their message was translated properly. Warning: This will increase translation character usage of the Free DeepL Account API.
- drillmode (memory): Fuel Rat Usage. Changes the plugin to monitor DrillSqueak instead of MechaSqueak. Used primarily for testing the plugin.
- debugmode (memory): Enables various debug missions to be printed to the /rawlog

--  
**/dl-debug**
```
/dl-debug
Args:
 - None
Example usage:
/dl-debug
```

Prints various information, useful for troubleshooting and debugging.

## Troubleshooting

**None of the commands work**  
Insure that File > Plugins shows the "adiIRC_DeepL.dll" plugin as "Loaded". Then fully close and restart your Adi client.

**Library was not found**  
If using Linux, make sure .NET 4.8 is installed with the same WINEPREFIX as AdiIRC  
If using Windows, make sure .NET 4.8 is installed  
Make sure that the Newtonsoft.dll is placed in the AdiIRC plugins directory.
