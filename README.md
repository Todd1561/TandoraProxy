* This program is used to interface between the pianobar-windows opensource application and any program that can send TCP commands.  
* This would generally be useful in a headless home automation A/V setup to bring Pandora to your sound system.  
* This application works by leveraging the Telnet server that can be enabled in Windows and remotely starting/sending commands to Pianobar. 
* I'll outline the basic steps below to get everything installed.

1. Enable the Windows Telnet service (3rd party telnet servers should work, too)
1. Set the Telnet service to start automatically in services.msc
1. Disable the idle timeout setting by entering "tlntadm.exe config timeoutactive=no" in an elevated command prompt
1. Download the latest pianobar-windows build from [pianobar-windows](https://github.com/thedmd/pianobar-windows/releases) and save it to a folder along with TandoraProxy.exe
1. Start TandoraProxy with the "/help" argument to see the arguments you'll need to specify.
1. Remember to open the TCP port you decide to use for TandoraProxy (1561 by default) on any relevant firewalls
1. Once the program is running you can send the below commands to control Pianobar.  These are just sent as raw ASCII via TCP.  You'll get a response back with the current status of Pianobar/TandoraProxy.

From here you can use whatever language you want that can work with TCP sockets to interace with TandoraProxy/Pianobar.
	
Commands:	
* "update": have TandoraProxy query pianobar-windows for the current song, station, play time and whether or not playback is active and a list of your Pandora stations.
* "playpause": toggle playing and pausing music playback.
* "next": play next song.
* "thumbsup": like current song.
* "thumbsdown": dislike current song.
* "change station <station mame>": change the current station to station <???>.
	
Questions or comments?

Todd Nelson

todd@toddnelson.net

[My Website](https://toddnelson.net)