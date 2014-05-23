# Ventriloquist
-----------------------

A text-to-speech API server for OS X and Windows

-----------------------

### Installation

Download the release for your platform.

#### OSX
Launch the application.

#### Windows 8
Run as Administrator on the initial launch, this will create the necessary urlacl's.  Subsequent runs can be any user.

-----------------------
Using either the system tray (Windows) or the menu bar (OS X), you can configure the server, as well as test the API.

A sample API call:

    			var client = new HttpClient ();
			client.PostAsync (new Uri("http://localhost:7888/api/tts"),
			                  new StringContent("{Text: 'The ripe taste of cheese improves with age.',Speed: 5,Language: 'en',Voice: 0,Interrupt: true}", System.Text.Encoding.UTF8, "application/json"));