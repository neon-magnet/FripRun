#!/usr/bin/env python3
"""
フリップ・ランの効果音6種類を合成して WAV に書き出す（jsfxr と同じ考え方の簡単な合成音）。
外部ライブラリは使わない。乱数のシードを固定しているので、毎回同じ音になる。

使い方（プロジェクトのルートで）:
    python3 Tools/generate_sfx.py
出力: Assets/Resources/Audio/sfx_*.wav（22050Hz・16bit・モノラル）
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio")


def square(phase):
    return 1.0 if (phase % 1.0) < 0.5 else -1.0


def triangle(phase):
    p = phase % 1.0
    return 4.0 * p - 1.0 if p < 0.5 else 3.0 - 4.0 * p


def sine(phase):
    return math.sin(2 * math.pi * phase)


def env(t, length, attack=0.005, release=None):
    """アタックと、終わりに向けての減衰。"""
    if t < attack:
        return t / attack
    if release is None:
        release = length - attack
    k = (length - t) / release
    return max(0.0, min(1.0, k))


def sweep(length, f0, f1, wave_fn, curve=1.0, vol=0.5, vibrato=0.0):
    n = int(RATE * length)
    out, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        k = (t / length) ** curve
        f = f0 + (f1 - f0) * k
        if vibrato:
            f *= 1.0 + vibrato * math.sin(2 * math.pi * 18 * t)
        phase += f / RATE
        out.append(wave_fn(phase) * env(t, length) * vol)
    return out


def noise(length, vol=0.5, lowpass=0.3, decay=1.0, seed=1):
    rnd = random.Random(seed)
    n = int(RATE * length)
    out, y = [], 0.0
    for i in range(n):
        t = i / RATE
        y += lowpass * (rnd.uniform(-1, 1) - y)
        out.append(y * vol * (env(t, length) ** decay))
    return out


def notes(freqs, each, wave_fn, vol=0.4, gap=0.0):
    out = []
    for f in freqs:
        out += sweep(each, f, f, wave_fn, vol=vol)
        out += [0.0] * int(RATE * gap)
    return out


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(n)]


def write(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-6, max(abs(s) for s in samples))
    gain = min(1.0, 0.9 / peak)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * gain)) * 32767)) for s in samples))
    print(f"{path}  {len(samples) / RATE:.2f}s")


def main():
    # 反転：短く上がる「ヒュッ」
    write("sfx_flip", sweep(0.12, 320, 960, square, curve=0.6, vol=0.35))
    # 着地：低く短い「トッ」
    write("sfx_land", mix(sweep(0.07, 160, 60, triangle, vol=0.6), noise(0.05, vol=0.3, lowpass=0.2, seed=2)))
    # コイン：2音の「ピコン」
    write("sfx_coin", notes([988, 1319], 0.06, square, vol=0.3) + sweep(0.08, 1319, 1319, square, vol=0.2))
    # ニアミス：風切り音＋高い音
    write("sfx_nearmiss", mix(noise(0.28, vol=0.5, lowpass=0.5, decay=1.5, seed=3),
                              sweep(0.22, 1400, 2200, sine, curve=0.5, vol=0.25, vibrato=0.02)))
    # 死亡：下がっていく「ブワッ」
    write("sfx_death", mix(sweep(0.45, 420, 50, square, curve=0.7, vol=0.35),
                           noise(0.4, vol=0.6, lowpass=0.15, decay=1.2, seed=4)))
    # ハイスコア更新：ドミソド
    write("sfx_highscore", notes([523, 659, 784], 0.08, square, vol=0.3) + sweep(0.3, 1047, 1047, square, vol=0.3, vibrato=0.01))


if __name__ == "__main__":
    main()
