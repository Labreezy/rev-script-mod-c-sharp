using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RevModLoader2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Frida.Script script;
        private Frida.Session session;
        private Frida.DeviceManager deviceman;
        private Frida.Device localdevice = null;
        private Frida.Process ggproc = null;
        private int loadcounter = 0;
        private String ggprocname = "GuiltyGearXrd";
        private String scriptsource = @"var xrdbase = Module.findBaseAddress('GuiltyGearXrd.exe');
		var fxnptr = xrdbase.add(0xB8BD60);
		var script = [];
		var scriptfound = false;
		var charabbrs = [ 'AXL', 'BED', 'CHP', 'DZY', 'ELP', 'FAU', 'INO', 'JAM', 'JHN', 'JKO', 'KUM', 'KYK', 'LEO', 'MAY', 'MLL', 'POT', 'RAM', 'RVN', 'SIN', 'SLY', 'SOL', 'VEN', 'ZAT'];
		var etcconst = '_ETC';
		var scriptpointer;
		var currscriptsize;
		var p1extramem = NULL;
		var p1etcmem = NULL;
		var p2extramem = NULL;
		var p2etcmem = NULL;
		var callcount = 0;
		var name;
		var attachaddr = xrdbase.add(0x9C8B2A);
		var scriptpointerpointer = NULL;
		Interceptor.attach(fxnptr, function (args){
				scriptpointerpointer = this.context.ecx.add(0x3C);
			})
		Interceptor.attach(fxnptr, {onEnter: function (args){
			callcount += 1;
			if(callcount == 1 && !(p1extramem.isNull() && p2extramem.isNull() && p1etcmem.isNull() && p2etcmem.isNull())){
				p1extramem = NULL;
				p2extramem = NULL;
				p1etcmem = NULL;
				p2etcmem = NULL;
			}
			if(callcount < 5){
				var intscript = [];
				var fxncount;
				if (callcount == 1 || callcount == 3){
				fxncount = Memory.readUInt(args[0]);
				name = Memory.readCString(args[0].add(0x24 * fxncount + 0x2C)).toUpperCase();
				}
				if(charabbrs.indexOf(name) != -1){
					if(callcount == 1 || callcount == 3){
						send(name, Memory.readByteArray(args[0], args[1].toInt32()));
					} else {
						send(name + etcconst, Memory.readByteArray(args[0], args[1].toInt32()));
					}
					var op = recv(function (value){
					    intscript = value.payload.split('-');
                        if(intscript.length > 1){
                            for(var i=0; i<intscript.length; i++) { intscript[i] = parseInt(intscript[i], 16); }
                            scriptfound = true
                        }
					})
					op.wait();
					if(scriptfound){
						var sizedifference = intscript.length - args[1].toInt32();
						if (sizedifference > 0x1FC){
							if(callcount == 1){
								p1extramem = Memory.alloc(intscript.length);
								Memory.writeByteArray(p1extramem, intscript);
								args[0] = p1extramem;
							} else if (callcount == 3){
								p2extramem = Memory.alloc(intscript.length);
								Memory.writeByteArray(p2extramem, intscript);
								args[0] = p2extramem;
							} else if (callcount == 2){
								p1etcmem = Memory.alloc(intscript.length);
								Memory.writeByteArray(p1etcmem, intscript);
								args[0] = p1etcmem;
							} else {
								p2etcmem = Memory.alloc(intscript.length);
								Memory.writeByteArray(p2etcmem, intscript);
								args[0] = p2etcmem;
							}
						} else {
							Memory.writeByteArray(args[0], intscript);
						}
						args[1] = ptr(intscript.length);
						scriptfound = false;
					}
					}
				}
				if(callcount == 6){
				callcount = 0;
				}
			}
		});";
        private String moddirectory = "ggmods";
        private String modfileext = ".ggscript";
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += new RoutedEventHandler(MainWindow_Loaded);
           
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(moddirectory))
            {
                Directory.CreateDirectory(moddirectory);
                MessageBox.Show("The program has created \"ggmods\" in the directory from where you ran this from.  Put all your .ggscript files there before clicking \"Load\".");
            }
            deviceman = new Frida.DeviceManager(Dispatcher);
            foreach (Frida.Device d in deviceman.EnumerateDevices())
            {
                if (d.Type.Equals(Frida.DeviceType.Local))
                {
                    localdevice = d;
                    break;
                }
            }
            if (localdevice == null)
            {
                MessageBox.Show("Error finding a local device.  Open an issue on github if this pops up.  If you're not sure how, contact @MemeMongerBPM on twitter or Labryz#5752 on discord and I'll be happy to help.");
                Application.Current.Shutdown();
            }

        }
        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            refreshButton.IsEnabled = false;
            infoLabel.Content = "Searching for Revelator Process...";
            foreach (Frida.Process p in localdevice.EnumerateProcesses())
            {
                if (p.Name.StartsWith(ggprocname))
                {
                    ggproc = p;
                    break;
                }
            }
            if(ggproc != null)
            {
                infoLabel.Content = "Found Revelator!  PID: " + ggproc.Pid.ToString() + "\r\nYou are ready to play your mods once you hit the load button!\r\nTo stop the mods, close this window and exit to a menu in-game.";
                loadButton.IsEnabled = true;
            } else
            {
                refreshButton.IsEnabled = true;
                infoLabel.Content = "Search failed.  Make sure Revelator is open and hit \r\n\"Refresh\" again once open.";
            }
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            session = localdevice.Attach(ggproc.Pid);
            script = session.CreateScript(scriptsource);
            script.Message += Script_Message;
            script.Load();
            loadButton.IsEnabled = false;
        }
        private string makeJSONmessageString(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            JsonTextWriter jtw = new JsonTextWriter(new StringWriter(sb));
            jtw.WriteStartObject();
            jtw.WritePropertyName("type");
            jtw.WriteValue("");
            jtw.WritePropertyName("payload");
            if (data != null)
            {
                jtw.WriteValue(BitConverter.ToString(data));
            } else
            {
                jtw.WriteValue("");
            }
            jtw.WriteEndObject();
            return sb.ToString();
        }
        private void Script_Message(object sender, Frida.ScriptMessageEventArgs e)
        {
            Frida.Script senderscript = (Frida.Script)sender;
            String jsonmessage = e.Message;
            String message = "";
            JsonTextReader jtr = new JsonTextReader(new StringReader(jsonmessage));
            while (jtr.Read())
            {
                if(jtr.TokenType == JsonToken.PropertyName && jtr.Value.Equals("error"))
                {
                    MessageBox.Show(jtr.ReadAsString());
                    Application.Current.Shutdown();
                }
                if(jtr.TokenType == JsonToken.PropertyName && jtr.Value.Equals("payload"))
                {
                    message = jtr.ReadAsString();
                    break;
                }
            }
            jtr.Close();
            String fn = message + modfileext;
            string scriptfilepath = moddirectory + "\\" + fn;
            if (infoLabel.Content.ToString().Contains("Revelator") || loadcounter == 4)
            {
                infoLabel.Content = "";
                loadcounter = 0;
            }
            if (File.Exists(scriptfilepath))
            {
                byte[] bytes = File.ReadAllBytes(scriptfilepath);
                String payload = message;
                senderscript.Post(makeJSONmessageString(bytes));
                loadcounter++;
                if (message.Contains("_ETC"))
                {
                    infoLabel.Content += fn + " loaded!\r\n";
                } else
                {
                    infoLabel.Content += fn + " loaded!\t";
                }
                
            } else
            {
                senderscript.Post(makeJSONmessageString(null));
                loadcounter++;
                if (message.Contains("_ETC"))
                {
                    infoLabel.Content += fn + " not loaded.\r\n";
                }
                else
                {
                    infoLabel.Content += fn + " not loaded.\t";
                }
                
            } 
        }
    }
}
