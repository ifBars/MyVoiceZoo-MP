namespace MvzMp.State;

internal sealed class ZooState
{
    public long Revision { get; set; }
    public long Gold { get; set; }
    public bool TutorialCompleted { get; set; }
    public bool WindIsland { get; set; }
    public bool DeepCave { get; set; }
    public int[] Camps { get; set; } = Array.Empty<int>();
    public int[] Costumes { get; set; } = Array.Empty<int>();
    public int EquippedCostume { get; set; }
    public AnimalState[] Animals { get; set; } = Array.Empty<AnimalState>();
}

internal sealed class AnimalState
{
    public int Id { get; set; }
    public bool Collected { get; set; }
    public string Name { get; set; } = "";
    public string Voice { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int SortingOrder { get; set; }
}

internal sealed class ZooCommand
{
    public string Kind { get; set; } = "";
    public int AnimalId { get; set; }
    public int Value { get; set; }
    public string Name { get; set; } = "";
    public string Lease { get; set; } = "";
    public VoiceData? Voice { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int SortingOrder { get; set; }
}

internal sealed class VoiceData
{
    public string Hash { get; set; } = "";
    public int Frequency { get; set; }
    public int Channels { get; set; }
    public int Samples { get; set; }
    public byte[] Pcm { get; set; } = Array.Empty<byte>();
}

internal sealed class WireMessage
{
    public string Kind { get; set; } = "";
    public string Request { get; set; } = "";
    public string Text { get; set; } = "";
    public string Lease { get; set; } = "";
    public int AnimalId { get; set; }
    public ZooState? State { get; set; }
    public ZooCommand? Command { get; set; }
    public VoiceData? Voice { get; set; }
    public ulong Peer { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool FacingRight { get; set; }
    public bool Moving { get; set; }
    public int CostumeId { get; set; }
}

