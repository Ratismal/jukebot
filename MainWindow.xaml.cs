using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using Discord;
using Discord.Audio;
using Microsoft.Win32;
using NAudio.Wave;

namespace Jukebot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            init();
            Closing += OnWindowClosing;
        }

        public static int volume = 15;
        private Discord discord;

        public void init()
        {
            discord = new Discord();
            Thread oThread = new Thread(new ThreadStart(discord.Start));
            oThread.Start();
            audioThread = new Thread(sendAudio);
            update();
        }

        public void update()
        {
            currentSong.Content = currentSongText;
            nextSong.Content = nextSongText;
            playingFolder.Content = playingFolderText;
            volumeBox.Text = volume.ToString();
            isPlayingFolder.Content = playingFolders.ToString();
        }

        public String musicToPlay;

        public string nextSongText = "null";
        public string currentSongText = "null";
        public string playingFolderText = "null";
        public bool playingFolders = false;
        public List<String> folderFiles = new List<String>();

        private void button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio Files (*.mp3)|*.mp3|All Files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                musicToPlay = openFileDialog.FileName;
                nextSongText = openFileDialog.SafeFileName;
                playingFolderText = musicToPlay.Replace(nextSongText, "");
            }
            generateFolders();
            update();
        }

        //play
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            currentSongText = nextSongText;
            audioThread.Abort();
            audioThread = new Thread(() => sendAudio());
            audioThread.Start();
            //sendAudio();
            update();
        }

        public void sendAudio()
        {
            discord._client.SetGame(currentSongText);
            Console.WriteLine("Setting game to " + currentSongText);
            var channelCount = discord._client.GetService<AudioService>().Config.Channels;
            // Get the number of AudioChannels our AudioService has been configured to use.
            var OutFormat = new WaveFormat(48000, 16, channelCount);


            // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
            using (var MP3Reader = new Mp3FileReader(musicToPlay))

                // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
            using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat))
                // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
            {
                resampler.ResamplerQuality = 60; // Set the quality of the resampler to 60, the highest quality
                int blockSize = OutFormat.AverageBytesPerSecond/50; // Establish the size of our AudioBuffer
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                    // Read audio into our buffer, and keep a loop open while data is present
                {
                    if (byteCount < blockSize)
                    {
                        // Incomplete Frame
                        for (int i = byteCount; i < blockSize; i++)
                            buffer[i] = 0;
                    }
                    buffer = adjustVolume(buffer, volume);
                    try
                    {
                        discord._vClient.Send(buffer, 0, blockSize); // Send the buffer to Discord
                    }
                    catch (Exception ex1)
                    {
                    }
                }

                Console.WriteLine("Song done");
                // discord._client.SetGame("");
                discord._client.SetGame(new Game());
                if (!playingFolders && !nextSongText.Equals("null") && !nextSongText.Equals(currentSongText))
                {
                    Console.WriteLine("meow");
                    currentSongText = nextSongText;
                    nextSongText = "null";
                    //update();
                    sendAudio();
                }
                else if (playingFolders)
                {
                    if (folderFiles.Count > 0)
                    {
                        Random rdm = new Random();
                        int choice = rdm.Next(0, folderFiles.Count);
                        musicToPlay = folderFiles.ElementAt(choice);
                        currentSongText = System.IO.Path.GetFileName(musicToPlay);
                        nextSongText = "null";
                        //update();
                        folderFiles.RemoveAt(choice);
                        sendAudio();
                    }
                    else
                    {
                        Console.WriteLine("Queue complete");
                    }
                }
            }
        }

        public Thread audioThread;


        private byte[] adjustVolume(byte[] audioSamples, float volume)
        {
            byte[] array = new byte[audioSamples.Length];
            for (int i = 0; i < array.Length; i += 2)
            {
                // convert byte pair to int
                short buf1 = audioSamples[i + 1];
                short buf2 = audioSamples[i];

                buf1 = (short) ((buf1 & 0xff) << 8);
                buf2 = (short) (buf2 & 0xff);

                short res = (short) (buf1 | buf2);
                res = (short) (res*(volume/100));

                // convert back
                array[i] = (byte) res;
                array[i + 1] = (byte) (res >> 8);
            }
            return array;
        }


        bool paused = false;
        //pause
        private void button2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!paused)
                {
                    audioThread.Suspend();
                }
                else
                {
                    audioThread.Resume();
                }
                paused = !paused;
                //   audioThread
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        //stop
        private void button3_Click(object sender, RoutedEventArgs e)
        {
            audioThread.Abort();
            discord._vClient.Clear();
            currentSongText = "null";
            update();
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // Handle closing logic, set e.Cancel as needed
            discord._client.Disconnect();

            //   if (discord._vClient ? null)
            discord._vClient?.Disconnect();
            System.Environment.Exit(0);
        }

        private void volumeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            volume = Int32.Parse(volumeBox.Text);
        }

        //refresh
        private void button4_Click(object sender, RoutedEventArgs e)
        {
            update();
        }

        //play folders
        private void button5_Click(object sender, RoutedEventArgs e)
        {
            playingFolders = !playingFolders;
            generateFolders();
            update();
        }

        public void generateFolders()
        {
            if (playingFolders && !playingFolderText.Equals("null"))
            {
                string[] files = Directory.GetFiles(playingFolderText);
                folderFiles.Clear();

                foreach (var file in files)
                {
                    if (file.EndsWith(".mp3") && !file.Equals(musicToPlay))
                    {
                        folderFiles.Add(file);
                    }
                }
            }
        }

        //avatar
        private void button6_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio Files (*.png)|*.png|All Files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                FileStream file = new FileStream(openFileDialog.FileName, FileMode.Open);
            //    var image = System.Drawing.Image.FromStream(file);
                discord.setAvatar(file);
                file.Close();
            }
        }

        //specific buttons

        //general
        private void button8_Click(object sender, RoutedEventArgs e)
        {
            playingFolderText = "C:\\Users\\Kirkland\\Music\\dnd\\ambience\\general\\";

            playingFolders = true;
            currentSongText = "null";
            nextSongText = "null";
            musicToPlay = "null";

            generateFolders();

            Random rdm = new Random();
            int choice = rdm.Next(0, folderFiles.Count);
            musicToPlay = folderFiles.ElementAt(choice);
            currentSongText = System.IO.Path.GetFileName(musicToPlay);
            nextSongText = "null";
            update();
            folderFiles.RemoveAt(choice);
            audioThread.Abort();
            audioThread = new Thread(sendAudio);
            audioThread.Start();
        }

        //battle
        private void button7_Click(object sender, RoutedEventArgs e)
        {
            playingFolderText = "C:\\Users\\Kirkland\\Music\\dnd\\ambience\\battle\\";


            playingFolders = true;
            currentSongText = "null";
            nextSongText = "null";
            musicToPlay = "null";

            generateFolders();
            
                Random rdm = new Random();
                int choice = rdm.Next(0, folderFiles.Count);
                musicToPlay = folderFiles.ElementAt(choice);
                currentSongText = System.IO.Path.GetFileName(musicToPlay);
                nextSongText = "null";
                update();
                folderFiles.RemoveAt(choice);
            audioThread.Abort();
            audioThread = new Thread(sendAudio);
            audioThread.Start();
                
            
        }
    }

    class Discord
    {
        //  static void Main(string[] args) => new Program().Start();
        public string token = "MTg4ODQzNDY5MjAzMDQ2NDAx.CjUixA.iKUQ4V3QBGrmQ4gdaQBlMdT_s4I";
        public DiscordClient _client;
        public IAudioClient _vClient;

        public void setAvatar(FileStream stream)
        {
            _client.ExecuteAndWait(async () =>
            {
                await _client.CurrentUser.Edit(token, avatar: stream).ConfigureAwait(false);
            });
        }

        public void Start()
        {
            Console.WriteLine("Initializing...");
            _client = new DiscordClient();


            _client.UsingAudio(x => // Opens an AudioConfigBuilder so we can configure our AudioService
            { x.Mode = AudioMode.Outgoing; // Tells the AudioService that we will only be sending audio
            });

            // _vClient = _client.GetService<AudioService>();


            _client.MessageReceived += async (s, e) =>
            {
                if (!e.Message.IsAuthor)
                {
                    Console.WriteLine(e.Message.User.Name + "> " + e.Message.Text);
                    if (e.Message.Text.StartsWith("!!"))
                    {
                        string command = e.Message.Text.Replace("!!", "");
                        //   if (command.Equals(""))
                        //      {
                        //                 await e.Channel.SendMessage("I am Jukebot. Do !!info for more details.");
                        //
                        //          }
                        string[] words = command.Split(' ');
                        switch (words[0])
                        {
                            case "info":
                                await
                                    e.Channel.SendMessage(
                                        "```diff\n!====== [Jukebot] ======!" +
                                        "\nA shitty ass music bot made by Ratismal (stupid cat)" +
                                        "\nIt plays music. lol what did u expect" +
                                        "\n!== [Features] ==!" +
                                        "\n+ a shitty looking unintuitive piece of shit GUI that only the host can see (lol)" +
                                        "\n+ plays music" +
                                        "\n!== [Commands] ==!" +
                                        "\n+ !!info - shows this" +
                                        "\n+ !!summon - summons bot to current voice channel" +
                                        "\n+ !!banish - tells the bot to piss off" +
                                        "\n+ !!volume - shows the current volume" +
                                        "\n+ !!volume <amount> - set the volume" +
                                        "\n+ !!volume +-<amount> - add/subtract the volume" +
                                        "\n!== [Conclusi] ==!" +
                                        "\n+ '-on' cut off from title for consistancy spacing sake" +
                                        "\n+ fuck my life" +
                                        "\n+ you want to play something? GOOD LUCK WITH THAT LOL ONLY I CAN" +
                                        "\n- This is red text!" +
                                        "\n- its really late i should go to bed lol fml smfh lmao kms" +
                                        "\n```");
                                break;
                            case "summon":
                                Console.WriteLine("Joining a voice channel");
                                await e.Channel.SendMessage("Joining channel");
                                var voiceChannel = e.User.VoiceChannel;
                                _vClient = await _client.GetService<AudioService>().Join(voiceChannel);
                                //await _vClient.Join(voiceChannel);
                                break;
                            case "banish":
                                Console.WriteLine("Leaving a voice channel");

                                await e.Channel.SendMessage("Leaving channel");
                                await _client.GetService<AudioService>().Leave(e.Server);
                                break;
                            case "volume":
                                if (words.Length == 1)
                                {
                                    e.Channel.SendMessage("Current volume: " + MainWindow.volume);
                                }
                                else
                                {
                                    //  string goodstuff;
                                    if (words[1].StartsWith("+") || words[1].StartsWith("-"))
                                    {
                                        int addVolume = Int32.Parse(words[1]);
                                        MainWindow.volume += addVolume;
                                        e.Channel.SendMessage("New volume: " + MainWindow.volume);
                                    }
                                    else
                                    {
                                        MainWindow.volume = Int32.Parse(words[1]);
                                        e.Channel.SendMessage("New volume: " + MainWindow.volume);
                                    }
                                }

                                break;
                            default:
                                await e.Channel.SendMessage("I am Jukebot. Do !!info for more details.");
                                break;
                        }
                    }
                }
            };

            _client.ExecuteAndWait(async () =>
            {
                await
                    _client.Connect(token
                        );
            });
            Console.WriteLine("Cool story bro (finished)");
        }
    }
}
