using Scenes;

if(args.Length==2&&args[0]=="--behavior-only"){Directory.CreateDirectory(args[1]);Behavior.Record(args[1]);return;}
Suite.Render(args.Length>0 ? args[0] : "artifacts/managed");
Behavior.Record(args.Length>0 ? args[0] : "artifacts/managed");
