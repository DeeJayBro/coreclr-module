using System;
using AltV.Net.Elements.Entities;

namespace AltV.Net.Elements.Refs
{
    public readonly struct VoiceChannelRef : IDisposable
    {
        private readonly IVoiceChannel voiceChannel;

        public bool Exists => voiceChannel != null;

        public VoiceChannelRef(IVoiceChannel voiceChannel)
        {
            this.voiceChannel = voiceChannel.AddRef() ? voiceChannel : null;
            Alt.Module.CountUpRefForCurrentThread(voiceChannel);
        }

        public void Dispose()
        {
            voiceChannel?.RemoveRef();
            Alt.Module.CountDownRefForCurrentThread(voiceChannel);
        }
    }
}