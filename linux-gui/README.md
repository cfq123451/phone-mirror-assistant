# 鎵嬫満鎶曞睆鍔╂墜 路 Linux GUI 鐗?
鍩轰簬 scrcpy / adb 鐨?Linux 鍥惧舰鐣岄潰鎵嬫満鎶曞睆宸ュ叿锛屾搷浣滈€昏緫涓?Windows 鐗堜竴鑷淬€?
## 瀹夎

```bash
# amd64 (x86_64)
sudo apt install -y ./phone-mirror-assistant_2.2.2-1_amd64.deb

# arm64 (鏍戣帗娲?/ ARM 鐩掑瓙 / 閾舵渤楹掗簾绛?
sudo apt install -y ./phone-mirror-assistant_2.2.2-1_arm64.deb
```

渚濊禆锛堣嚜鍔ㄥ畨瑁咃級锛歚python3-pyside6 adb scrcpy`锛堥渶 Debian 12+ / Ubuntu 22.04+ / 鏍戣帗娲?OS锛夈€?
瀹夎鍚庝粠搴旂敤鑿滃崟鐐瑰嚮銆屾墜鏈烘姇灞忓姪鎵嬨€嶅惎鍔紝鎴栫粓绔繍琛?`phone-mirror-assistant`銆?
## 浣跨敤

- **璁惧鍒楄〃**锛氳嚜鍔ㄨ瘑鍒?USB / 鏃犵嚎璁惧锛堣繛鎺ユ柟寮忓窘绔犲尯鍒嗭級锛屽悓涓€鍙拌澶囧悓鏃跺湪绾胯嚜鍔ㄥ幓閲嶏紱姣忚銆屾姇灞忋€嶆寜閽竴閿姇灞?- **鏃犵嚎閰嶅**锛氱偣銆屾棤绾块厤瀵广€嶁啋 濉墜鏈轰笂鏄剧ず鐨?IP / 绔彛 / 6 浣嶉厤瀵圭爜 鈫?鍚庡彴鑷姩 `adb pair`
- **USB 杩炴帴**锛氱偣銆孶SB 杩炴帴銆嶁啋 鏈彃绾跨粰鍑轰笁鏉℃帓鏌ユ彁绀猴紱宸茶繛鎺ュ脊鍑恒€岃繛鎺ュ埌 xxx 鎴愬姛锛屾槸鍚︾‘璁ゆ姇灞忥紵銆?- **寮€濮?/ 鍋滄鎶曞睆**锛氫富鐣岄潰涓€閿帶鍒讹紱鍚姩鏃舵娴嬪埌璁惧鑷姩鎶曞睆
- **绐楀彛璁剧疆**锛氱獥鍙ｅ搴︼紙300~1440锛夈€佺疆椤躲€佹棤杈规
- **鎵樼洏**锛氬叧闂獥鍙ｉ┗鐣欑郴缁熸墭鐩橈紝鍙抽敭鍙揩閫熸搷浣?- **閰嶇疆**锛歚~/.config/phone-mirror-assistant/config.ini`锛堥娆¤繍琛岃嚜鍔ㄧ敓鎴愶級
- **涓枃杈撳叆**锛氶娆″湪鎵嬫満杈撳叆娉曢噷閫?`English(US)`锛屼箣鍚庣數鑴戞墦瀛椾腑鏂囨甯?
## 鎵撳寘

```bash
bash build_deb.sh all        # 鐢熸垚 amd64 + arm64 涓や釜 deb 鍒?dist/
bash build_deb.sh amd64      # 鍙墦 amd64
bash build_deb.sh arm64      # 鍙墦 arm64
```

## 寮€鍙戣繍琛?
```bash
pip install PySide6          # 鎴?apt install python3-pyside6
python3 phone-mirror-gui.py
```

闇€瑕佺郴缁熷凡瀹夎 `adb` 涓?`scrcpy`銆?