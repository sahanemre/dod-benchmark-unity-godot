class_name S2Validation extends RefCounted

## S2 dogrulama kosusunun ciktisi: kare basina cift sayilari.
## Bicim: frame,pairs,responses[,pairs_bruteforce]
## .tmp'ye yazilip yeniden adlandirilir (yarim dosya "tamam" sanilmasin).


static func write(path: String, lines: PackedStringArray, with_bruteforce: bool) -> bool:
	var header := "frame,pairs,responses" + (",pairs_bruteforce" if with_bruteforce else "")
	var dir := path.get_base_dir()
	if not dir.is_empty() and not DirAccess.dir_exists_absolute(dir):
		DirAccess.make_dir_recursive_absolute(dir)
	var tmp := path + ".tmp"
	var f := FileAccess.open(tmp, FileAccess.WRITE)
	if f == null:
		push_error("[S2] Dogrulama dosyasi acilamadi: " + tmp)
		return false
	f.store_string(header + "\n" + "\n".join(lines) + "\n")
	f.close()
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)
	return DirAccess.rename_absolute(tmp, path) == OK
