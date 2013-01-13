using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using NAudio.Wave;
using NAudio.FileFormats;
using NAudio.CoreAudioApi;
using NAudio;
using CUETools.Codecs;
using CUETools.Codecs.FLAKE;
using CUETools;

namespace Computer_Speech_Control
{
    public partial class Form1 : Form
    {
        WaveIn waveIn;
        WaveFileWriter writer;
        List<byte[]> history_bytes = new List<byte[]>();

        int rec_times = 0;

        string result = "";

        int writeToFile = 0;

        int volume = 0;
        int volume_level = 10;
        int tick_count = 0;

        int n = 0;
        int s = 0;

        public Form1()
        {
            InitializeComponent();
        }

        void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (writeToFile == 1)
            {
                rec_times++;
            }

            for (int index = 0; index < e.BytesRecorded; index += 2)
            {
                short sample = (short)((e.Buffer[index + 1] << 8) |
                                        e.Buffer[index + 0]);
                float sample32 = sample / 32768f;
                ProcessSample(sample32);

                writer.WriteSample(sample32);
            }
        }

        private void ProcessSample(float sample)
        {
            n++;
            if (n > 100)
            {
                volume = s / 100;
                progressBar1.Value = volume;
                n = 0;
                s = 0;
            }
            else
                s += (int)Math.Abs((sample * 100));
        }

        void StopRecording()
        {
            waveIn.StopRecording();
        }

        private void waveIn_RecordingStopped(object sender, EventArgs e)
        {
            if (writeToFile == 1)
            {
                waveIn.Dispose();
                waveIn = null;
                writer.Close();
                writer = null;

                int bytes_to_read = (rec_times + 1) * 6400;
                byte[] wav_bytes = new byte[bytes_to_read];

                WaveFileReader wfr = new WaveFileReader("file.wav");

                if (wfr.Length < bytes_to_read)
                {
                    wfr.Read(wav_bytes, 0, (int)wfr.Length);
                }
                else
                {
                    wfr.Position = wfr.Length - 1 - bytes_to_read;
                    wfr.Read(wav_bytes, 0, bytes_to_read);
                    
                }
                wfr.Dispose();
                wfr.Close();

                WaveIn second_waveIn = new WaveIn();
                second_waveIn.DeviceNumber = 0;
                second_waveIn.WaveFormat = new WaveFormat(16000, 2);
                WaveFileWriter second_writer = new WaveFileWriter("cmd.wav", second_waveIn.WaveFormat);
                second_waveIn.StartRecording();
                second_writer.Write(wav_bytes, 0, bytes_to_read);
                second_waveIn.StopRecording();

                second_waveIn.Dispose();
                second_waveIn = null;
                second_writer.Close();
                second_writer = null;

                listBox1.Items.Add("CONVERTING");
                listBox1.SelectedIndex = listBox1.Items.Count - 1;

                Wav2Flac("cmd.wav", "file.flac");

                result = GoogleSpeechRequest(16000);
                string res = result;
                int k = res.IndexOf("utterance\":\"") + "utterance\":\"".Length;
                int k1 = res.IndexOf("\"", k + 1);
                string cmd = res.Substring(k, k1 - k);
                listBox1.Items.Add("RECOGNIZED");
                richTextBox1.Text += cmd + "\n";



                File.Delete("cmd.wav");
                rec_times = 0;
                writeToFile = 0;
            }
            else

                if (writeToFile == 0)
                {
                    waveIn.Dispose();
                    waveIn = null;
                    writer.Close();
                    writer = null;
                }
        }

        public static int Wav2Flac(String wavName, string flacName)
        {
            int sampleRate = 0;

            IAudioSource audioSource = new WAVReader(wavName, null);

            AudioBuffer buff = new AudioBuffer(audioSource, 0x10000);

            FlakeWriter flakewriter = new FlakeWriter(flacName, audioSource.PCM);
            sampleRate = audioSource.PCM.SampleRate;

            FlakeWriter audioDest = flakewriter;
            while (audioSource.Read(buff, -1) != 0)
            {
                audioDest.Write(buff);
            }

            audioDest.Close();
            audioSource.Close();

            return sampleRate;
        }

        public static int Wav2Flac(Stream streamName, string flacName)
        {
            int sampleRate = 0;

            IAudioSource audioSource = new WAVReader(null, streamName);

            AudioBuffer buff = new AudioBuffer(audioSource, 0x10000);

            FlakeWriter flakewriter = new FlakeWriter(flacName, audioSource.PCM);
            sampleRate = audioSource.PCM.SampleRate;

            FlakeWriter audioDest = flakewriter;
            while (audioSource.Read(buff, -1) != 0)
            {
                audioDest.Write(buff);
            }

            audioDest.Close();
            audioSource.Close();

            return sampleRate;
        }


        public String GoogleSpeechRequest(int sampleRate)
        {

            WebRequest request = WebRequest.Create("https://www.google.com/speech-api/v1/recognize?xjerr=1&client=chromium&lang=ru-RU");

            request.Method = "POST";

            byte[] buffer = File.ReadAllBytes("file.flac");

            request.ContentType = "audio/x-flac; rate=" + sampleRate; //"16000"      
            request.ContentLength = buffer.Length;

            Stream dataStream = request.GetRequestStream();

            dataStream.Write(buffer, 0, buffer.Length);

            dataStream.Close();
            WebResponse response = request.GetResponse();

            dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();

            reader.Close();
            dataStream.Close();
            response.Close();

            return responseFromServer;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (waveIn == null)
            {

                try
                {
                    waveIn = new WaveIn();

                    waveIn.DeviceNumber = 0;

                    waveIn.DataAvailable += waveIn_DataAvailable;
                    waveIn.RecordingStopped += new EventHandler<StoppedEventArgs>(waveIn_RecordingStopped);

                    waveIn.WaveFormat = new WaveFormat(16000, 2);
                    writer = new WaveFileWriter("file.wav", waveIn.WaveFormat);

                    listBox1.Items.Add("RECORDING TO FILE");
                    listBox1.SelectedIndex = listBox1.Items.Count - 1;

                    waveIn.StartRecording();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                };
            }
            else
            {
                if (writeToFile == 1)
                {
                    if (tick_count < 10)
                    {
                        if (volume > volume_level)
                        {
                            listBox1.Items.Add("VOLUME > VOLUME_LEVEL: " + volume);
                            listBox1.SelectedIndex = listBox1.Items.Count - 1;
                            tick_count = 0;
                        }
                        tick_count++;
                    }
                    else
                    {
                        listBox1.Items.Add("VOLUME LOST. END.");
                        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        waveIn.StopRecording();
                        tick_count = 0;
                        //writeToFile = 3;
                    }
                }
                else
                    if (writeToFile == 0)
                    {
                        if (volume > volume_level)
                        {
                            listBox1.Items.Add("VOLUME > VOLUME_LEVEL: " + volume);
                            listBox1.SelectedIndex = listBox1.Items.Count - 1;
                            writeToFile = 1;
                        }
                        if ((new FileInfo("file.wav")).Length >= 1024 * 1024 * 5)
                        {
                            listBox1.Items.Add("reset wav file");
                            listBox1.SelectedIndex = listBox1.Items.Count - 1;
                            waveIn.StopRecording();
                        }
                    }
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            volume_level = (int)numericUpDown1.Value;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            waveIn = new WaveIn();

            waveIn.DeviceNumber = 0;

            waveIn.DataAvailable += waveIn_DataAvailable;
            waveIn.RecordingStopped += new EventHandler<StoppedEventArgs>(waveIn_RecordingStopped);

            waveIn.WaveFormat = new WaveFormat(16000, 2);
            writer = new WaveFileWriter("file.wav", waveIn.WaveFormat);

            listBox1.Items.Add("RECORDING TO FILE");
            listBox1.SelectedIndex = listBox1.Items.Count - 1;

            waveIn.StartRecording();
            timer1.Enabled = true;
        }
    }
}
