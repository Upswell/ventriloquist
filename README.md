# Ventriloquist
-----------------------

A text-to-speech API server for OS X and Windows

-----------------------

### Installation

Download the release for your platform.

#### OSX

The application is unsigned, so you will have to make sure you have System Preferences->Security & Privacy->General->Allow applications downloaded from: Anywhere

Launch the application.

#### Windows 8
Run as Administrator on the initial launch, this will create the necessary urlacl's.  Subsequent runs can be any user.

-----------------------
Using either the system tray (Windows) or the menu bar (OS X), you can configure the server, as well as test the API.

Configuration data is stored in a SQLite database.  The database locations for each platform are:

OSX: ~/Library/Application Support/Ventriloquist/config.db
Windows: %HOME%/AppData/Local/Ventriloquist/config.db

### API
Endpoint: /api/tts

The API for TTS accepts a JSON object with the following parameters:

Variable | Mandatory | Default | Description
--- | --- | --- | ---
``Text`` | Y | - | Text to be spoken by the speech synthesizer.
``Speed`` | N | 5 | The voice rate. Valid range is 0-10
``Language`` | N | "en" | Language options are currently "en" and "fr"
``Voice`` | N | 0 | Index of the voice you want to use

A sample API call:

    var client = new HttpClient ();
    client.PostAsync (new Uri("http://localhost:7888/api/tts"),
			          new StringContent("{Text: 'The ripe taste of cheese improves with age.',Speed: 5,Language: 'en',Voice: 0,Interrupt: true}", System.Text.Encoding.UTF8, "application/json"));