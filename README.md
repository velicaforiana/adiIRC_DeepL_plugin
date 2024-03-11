# AdiIRC Deepl Plugin

This plugin allows to mark nicknames for automatic translation to english using the DeepL API.
A DeepL API Key is required and can be set using the /dl-api command.
/dl-help will list all supported commands.
The most important one would be /dl-mon <nick> to mark that nickname for translation.

In addition to the general translation feature there is some Fuelrats(fuelrats.com) integration as well.
Incoming cases and their language can be identified and marked for tranlation if needed.
Fellow Fuelrat Dispatchers keep in mind that if you use the /dl-mecha option it can be tempting to dispatch
the entire case in the clients native language, but if they talk english themself that is always preferable.

# Installation

 - Make sure you have .NET installed. This plugin is compiled against .NET 4.8 (AdiIRC is using 4.5)
 - Place the content of the release package (2 DLL files) into the plugin folder of your AdiIRC installation
 - In AdiIRC select Files -> Plugins. Then select "Install new" and select "adiIRC_DeepL.dll" next.
