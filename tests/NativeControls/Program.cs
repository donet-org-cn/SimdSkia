using SimdSkia.NativeControls;

int checks=0;
void Check(bool result){checks++;if(!result)throw new InvalidOperationException($"Check {checks} failed");}
var editor=new LineEditor();
editor.SelectAll();Check(editor.Insert("中文 AB",_=>true,out _));
editor.MoveTo(3);editor.Right(true);Check(editor.Selected=="A");
Check(editor.Insert("C",_=>true,out _));Check(editor.Text=="中文 CB");
editor.Delete(true);Check(editor.Text=="中文 B");
editor.Delete(false);Check(editor.Text=="中文 ");
editor.SelectAll();Check(editor.Insert("\U00020000A",_=>true,out _));
editor.MoveTo(1);Check(editor.Caret==0);
editor.Right(false);Check(editor.Caret==2);
editor.Delete(true);Check(editor.Text=="A");
editor.SelectAll();Check(!editor.Insert(new string('a',129),_=>true,out _));Check(editor.Text=="A");
Check(!editor.Insert("\ud800",_=>true,out _));Check(editor.Text=="A");
Check(!editor.Insert("e\u0301",_=>true,out _));Check(editor.Text=="A");
Check(!editor.Insert("中",_=>false,out _));Check(editor.Text=="A");
Check(editor.Insert("a\r\nb\tc",_=>true,out _));Check(editor.Text=="a b c");
editor.MoveTo(0);editor.Left(false);editor.Delete(true);Check(editor.Caret==0&&editor.Text=="a b c");
editor.MoveTo(editor.Text.Length);editor.Right(false);editor.Delete(false);Check(editor.Text=="a b c");
Console.WriteLine($"PASS: {checks} text editor Unicode/selection/boundary checks.");
