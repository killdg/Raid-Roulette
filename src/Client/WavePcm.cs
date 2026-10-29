using System;
using System.IO;
using System.Text;
namespace RaidRoulette.TestingClient;

public sealed class WavePcm
{
 public int Rate,Channels;public float[] Samples=Array.Empty<float>();
 public static WavePcm Read(string path)
 {
  using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream,Encoding.ASCII);
  if(stream.Length<44||stream.Length>32_000_000||new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Expected a PCM WAV file");
  reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Missing WAVE header");
  int channels=0,rate=0,bits=0,format=0;long dataAt=0;int dataLength=0;
  while(stream.Position+8<=stream.Length)
  {
   string chunk=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long end=stream.Position+size;
   if(end>stream.Length)throw new InvalidDataException("Truncated WAV chunk");
   if(chunk=="fmt "){if(size<16)throw new InvalidDataException("Invalid WAV format");format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();reader.ReadUInt16();bits=reader.ReadUInt16();}
   else if(chunk=="data"){dataAt=stream.Position;dataLength=checked((int)size);}
   stream.Position=end+(size%2);
  }
  if(format!=1||bits!=16||channels is <1 or >2||rate is <8000 or >96000||dataAt==0||dataLength==0||dataLength%(channels*2)!=0)throw new InvalidDataException("Expected 16-bit mono/stereo PCM WAV");
  stream.Position=dataAt;var bytes=reader.ReadBytes(dataLength);var samples=new float[dataLength/2];
  for(int i=0;i<samples.Length;i++)samples[i]=(short)(bytes[i*2]|bytes[i*2+1]<<8)/32768f;
  return new(){Rate=rate,Channels=channels,Samples=samples};
 }
}

public static class StormAudioPolicy
{
 public const string RainSourceName="RR_StormtideRecordedRain";
 public static double NativeRain(double blend)=>Math.Cos(Math.Max(0,Math.Min(1,blend))*Math.PI/2);
 public static double RecordedRain(double blend)=>Math.Sin(Math.Max(0,Math.Min(1,blend))*Math.PI/2);
 public static int ChooseThunder(int previous,int count,double roll)
 {
  if(count<=0)return -1;
  int choices=previous>=0&&previous<count&&count>1?count-1:count;
  int selected=Math.Min(choices-1,(int)(Math.Max(0,Math.Min(1,roll))*choices));
  return choices<count&&selected>=previous?selected+1:selected;
 }
}
