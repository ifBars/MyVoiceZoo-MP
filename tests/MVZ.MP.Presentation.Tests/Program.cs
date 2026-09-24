using MvzMp.Presentation;
static PlayerPose Pose(float x, float y, double time) => new(x,y,0,true,true,0,time);
static void Near(float actual, float expected, string name) { if (Math.Abs(actual-expected) > .001) throw new Exception($"{name}: {actual} != {expected}"); }
var buffer = new PoseBuffer();
buffer.Add(Pose(0,0,10), 100);
buffer.Add(Pose(1,0,10.1),100.13);
buffer.Add(Pose(1,1,10.2),100.21);
Near(buffer.Sample(100.20).X,.5f,"jitter uses sender time");
var corner = buffer.Sample(100.30);
Near(corner.X,1,"corner follows received path"); Near(corner.Y,.5f,"corner second segment");
var held = buffer.Sample(101);
Near(held.X,1,"packet loss holds x"); Near(held.Y,1,"packet loss never predicts through wall");
if (buffer.Add(Pose(-1,0,10.1),101)) throw new Exception("stale sample accepted");
buffer.Add(Pose(20,0,11.2),102);
Near(buffer.Sample(102).X,20,"teleport snaps instead of crossing map");
if (buffer.Add(Pose(float.NaN,0,12),103)) throw new Exception("nonfinite accepted");
Console.WriteLine("PASS pose buffering: jitter, corner, packet loss, stale, teleport, invalid coordinates");

var appearance = new PoseBuffer();
appearance.Add(Pose(0,0,10) with { CharacterId = 1, CostumeId = 2 }, 100);
appearance.Add(Pose(1,0,10.1) with { CharacterId = 0, CostumeId = 3 }, 100.1);
var interpolated = appearance.Sample(100.2);
if (interpolated.CharacterId != 1 || interpolated.CostumeId != 2)
    throw new Exception("interpolation changed appearance before its sample time");
var switched = appearance.Sample(100.3);
if (switched.CharacterId != 0 || switched.CostumeId != 3)
    throw new Exception("new character and costume did not arrive together");
if (appearance.Sample(101).CharacterId != 0)
    throw new Exception("packet loss changed character identity");
if (Pose(0,0,0).CharacterId != 0)
    throw new Exception("default character must remain Lucy");
Console.WriteLine("PASS appearance buffering: identity and costume stay paired across interpolation and packet loss");
