"""Builds/WebGL をローカルで配信する（unityroom と同じく Gzip 圧縮・Decompression Fallback オフのビルド用）。

*.gz には Content-Encoding: gzip を付け、元の拡張子の Content-Type で返す。
使い方: python3 Tools/serve_webgl.py [port] [dir]
"""
import http.server
import os
import sys

TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".data": "application/octet-stream",
}


class Handler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".gz"):
            base = path[:-3]
            return TYPES.get(os.path.splitext(base)[1], "application/octet-stream")
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split("?")[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
    root = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "Builds", "WebGL")
    os.chdir(root)
    print(f"Serving {os.getcwd()} on http://localhost:{port}/")
    http.server.ThreadingHTTPServer(("", port), Handler).serve_forever()
