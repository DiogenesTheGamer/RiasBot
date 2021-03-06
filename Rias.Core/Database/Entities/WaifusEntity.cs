﻿namespace Rias.Core.Database.Entities
{
    public class WaifusEntity : DbEntity, IWaifusEntity
    {
        public CharactersEntity? Character { get; set; }
        public CustomCharactersEntity? CustomCharacter { get; set; }
        
        public ulong UserId { get; set; }
        public int? CharacterId { get; set; }
        public int? CustomCharacterId { get; set; }
        
        public string? Name => Character?.Name ?? CustomCharacter?.Name;

        public string? ImageUrl => Character?.ImageUrl ?? CustomCharacter?.ImageUrl;
        public string? CustomImageUrl { get; set; }
        public int Price { get; set; }
        public bool IsSpecial { get; set; }
        public int Position { get; set; }
    }
}