# Drone Jobs VR

Quest 2向けの短時間ドローン仕事体験ゲームです。配送、捜索救助、建物点検を、操縦者・フルFPV・三人称追従の3視点で遊べます。

実機ビルド、VR表示確認、Store申請は [DEV.md](DEV.md) を参照してください。

## 起動

1. Unity 6 (`6000.0.66f2` 以降)でこのフォルダを開く。
2. Package Managerで依存パッケージの解決を待つ。
3. `Project Settings > XR Plug-in Management > Android`でOpenXRを有効化し、Oculus Touch Controller Profileを追加する。
4. `Assets/Scenes/Prototype.unity`を開いてPlayする。
5. Quest 2実機ではAndroid / ARM64でBuild & Runする。

## 操作

- Quest: 左スティック＝高度・ヨー、右スティック＝移動、右トリガー＝仕事操作
- A / 1：配送、X / 2：捜索救助、右スティック押し込み / 3：建物点検
- Y / M：アシスト／マニュアル切替（待機中）、サーマル切替（飛行中）
- 右グリップ / C：操縦者 → フルFPV → 三人称追従の順に視点切替
- B / R：自動帰還・着陸、着陸後にもう一度押すと離陸
- キーボード検証：左 `Q/E/A/D`、右 `W/S/J/L`、`Space`、`R`

## 試作の範囲

共有災害対応マップ、3種類の仕事、3視点、二つの飛行モード、サーマル表示、バッテリー・ジオフェンス、採点とローカルベストスコアを含みます。オンライン機能と機体改造は含めません。
