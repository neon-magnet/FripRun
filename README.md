# FlipRun

ワンボタンで重力を上下に切り替えて走る、エンドレスランナーです（Unity 6 / WebGL、unityroom 向け）。
仕様は [flip-run.md](flip-run.md) にあります。Unity プロジェクトは `FlipRun/` です。

## ビルド

```bash
cd FlipRun
/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWebGL -logFile build.log
```

- 出力先は `FlipRun/Builds/WebGL` です（git には含めません）。
- 設定は `BuildScript.ApplyWebGLSettings` で毎回そろえます：
  - Development Build オフ
  - Gzip
  - Decompression Fallback オフ
  - Explicitly Thrown Exceptions Only
  - Managed Stripping Medium
  - 960×540
  - スプラッシュ画面なし
- Decompression Fallback は、仕様書ではオンですが、unityroom ヘルプ「unityroomにアップロードするためのビルド方法」の指定に合わせてオフにしています。

ローカルで遊ぶには、次のコマンドでサーバーを起動して http://localhost:8765/ を開きます。`.gz` に `Content-Encoding: gzip` を付けて配信するので、Fallback オフのビルドでも動きます。

```bash
python3 FlipRun/Tools/serve_webgl.py
```

## テスト

Unity の Test Runner、またはコマンドラインの `-runTests -testPlatform EditMode` / `PlayMode` で実行します。

## unityroom への投稿（限定公開）

1. unityroom にログインし、ゲームを新しく登録します（投稿には X（Twitter）アカウントが必要です）。
2. WebGL のアップロード欄に、`FlipRun/Builds/WebGL/Build/` の4ファイルを入れます（フォルダごとのドラッグ＆ドロップでも可）。
   - `WebGL.loader.js`
   - `WebGL.framework.js.gz`
   - `WebGL.data.gz`
   - `WebGL.wasm.gz`
3. 画面サイズは **960 × 540** にします。
4. 公開設定は **限定公開** にして、URL を友人に送ります。
5. 下の紹介文を貼り付けます。

### 紹介文（そのまま貼れる形）

**タイトル**：FLIP RUN

**説明**
重力をひっくり返して、床と天井を走り抜けろ！
ボタンひとつで重力が上下に入れ替わる、ワンボタンのエンドレスランナーです。
トゲや穴を避けて、どこまで遠くへ行けるか挑戦しよう。走るほどスピードが上がります。

**操作方法**
- クリック / スペース：重力を反転（床か天井に立っているときだけ）
- R：リトライ
- M：音のオン／オフ

**ヒント**
- 着地の直前に押しても反転が出ます（先行入力）。
- 足場から落ちた直後でも、少しのあいだは反転できます。
- トゲすれすれを抜けると「ニアミス！」で +5 点。
- コインは 1枚 10 点です。

**クレジット**
- フォント：Noto Sans JP（SIL Open Font License 1.1）
- ドット絵：SpriteCook で生成
- 効果音：自作（プログラムで合成）
