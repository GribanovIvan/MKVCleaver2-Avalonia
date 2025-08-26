using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using NEbml.Core;

namespace MKVCleaver2
{
    public class MkvFile : INotifyPropertyChanged
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public List<Track> Tracks { get; set; } = new List<Track>();
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                _isSelected = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); 
            } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class Track : INotifyPropertyChanged
    {
        public int Number { get; set; }
        public string UID { get; set; } = "";
        public string Type { get; set; } = "";
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "";
        public string Name { get; set; } = "";
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                _isSelected = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); 
            } 
        }
        
        public MkvFile Parent { get; set; } = null!;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class BatchTrack : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public Track Track { get; set; } = null!;
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                _isSelected = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); 
            } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class TrackComparer : IEqualityComparer<Track>
    {
        public bool Equals(Track? x, Track? y)
        {
            if (x == null || y == null) return false;
            return x.Type == y.Type && x.Codec == y.Codec && x.Language == y.Language;
        }

        public int GetHashCode(Track obj)
        {
            return obj.Type.GetHashCode() ^ obj.Codec.GetHashCode() ^ obj.Language.GetHashCode();
        }
    }

    public class TagElement
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public class EbmlElement
    {
        public List<EbmlElement> Elements { get; set; } = new List<EbmlElement>();

        public EbmlElement? this[string name]
        {
            get
            {
                return Elements.FirstOrDefault(x => x.ToString() == name);
            }
        }
    }

    public class EbmlParser
    {
        public List<Track> Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<Track>();

            if (input.Contains("Помилка") || input.Contains("Error"))
                return new List<Track>();

            List<Track> tracks = new List<Track>();
            string[] lines = input.Split('\n');
            
            Track? currentTrack = null;
            bool inTagsSection = false;
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                
                if (line.Contains("|+ Теги"))
                {
                    inTagsSection = true;
                    continue;
                }
                
                if (inTagsSection)
                    continue;
                
                if (line.Contains("Номер доріжки:"))
                {
                    currentTrack = new Track();
                    
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"Номер доріжки:\s*(\d+).*?mkvextract:\s*(\d+)");
                    if (match.Success)
                    {
                        currentTrack.Number = int.Parse(match.Groups[2].Value);
                    }
                    
                    currentTrack.Type = "Unknown";
                    currentTrack.Codec = "Unknown";
                    currentTrack.Language = "und";
                    currentTrack.Name = "";
                    
                    tracks.Add(currentTrack);
                }
                else if (currentTrack != null && line.Contains("Тип доріжки:"))
                {
                    if (line.Contains("відео"))
                        currentTrack.Type = "Video";
                    else if (line.Contains("аудіо"))
                        currentTrack.Type = "Audio";
                    else if (line.Contains("субтитри"))
                        currentTrack.Type = "Subtitle";
                }
                else if (currentTrack != null && line.Contains("ID кодека:"))
                {
                    var codecMatch = System.Text.RegularExpressions.Regex.Match(line, @"ID кодека:\s*(.+)$");
                    if (codecMatch.Success)
                    {
                        currentTrack.Codec = codecMatch.Groups[1].Value.Trim();
                    }
                }
                else if (currentTrack != null && line.Contains("Мова:"))
                {
                    var langMatch = System.Text.RegularExpressions.Regex.Match(line, @"Мова:\s*(.+)$");
                    if (langMatch.Success)
                    {
                        currentTrack.Language = langMatch.Groups[1].Value.Trim();
                    }
                }
                else if (currentTrack != null && line.Contains("Назва:"))
                {
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(line, @"Назва:\s*(.+)$");
                    if (nameMatch.Success)
                    {
                        currentTrack.Name = nameMatch.Groups[1].Value.Trim();
                    }
                }
            }
            
            foreach (var track in tracks)
            {
                if (string.IsNullOrEmpty(track.Name))
                {
                    track.Name = $"{track.Type} Track {track.Number}";
                }
            }
            
            return tracks;
        }
    }
}
