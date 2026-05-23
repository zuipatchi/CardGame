---
description: ヒアリング→実装→テストの順で新機能を実装する
---

# feature: 新機能実装

## Phase 1: ヒアリング・設計確認

1. `CLAUDE.md` と `docs/` を読み、関連するコードを調べる
2. 以下をユーザーに提示して**しっかり承認を得てから次へ進む**:
   - 何を作るか
   - テストで検証する項目

## Phase 2: 実装

承認された設計に従ってコードを書く。`CLAUDE.md` のコーディング規約に従うこと。

## Phase 3: テスト

コードの後にテストを書く。

- **PlayMode テスト**: シーン・UI を伴うもの（中心）
  - `[UnityTearDown]` で `CommonSceneLoader._loaded` を reflection でリセット
  - ボタンクリック模擬は `NavigationSubmitEvent`
  - テストメソッド名は日本語
- **EditMode テスト**: 純粋ロジックのみ
  - asmdef に Common の GUID と `R3.dll` を `precompiledReferences` に追加が必要
  - `ReadOnlyReactiveProperty<T>` の値は `.CurrentValue`（`.Value` は不可）
  - R3 の Subscribe 拡張メソッドには `using R3;` が必要

## Phase 4: 動作確認の案内

Unity Editor での確認方法をユーザーに伝える:
- 通常確認: Play ボタン
- EditMode テスト: Window → General → Test Runner → EditMode → Run All
- PlayMode テスト: Window → General → Test Runner → PlayMode → Run All
