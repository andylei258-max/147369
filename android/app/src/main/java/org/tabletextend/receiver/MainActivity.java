package org.tabletextend.receiver;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Bundle;
import android.text.InputType;
import android.view.View;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import org.json.JSONObject;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

public final class MainActivity extends Activity {
    private DisplayView display;
    private LinearLayout panel;
    private EditText host, port, token;
    private Button connect;
    private volatile int generation;
    private volatile boolean desired;
    private volatile Socket socket;
    private String address, pairing;
    private int tcpPort;
    private volatile boolean active;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        SharedPreferences saved = getSharedPreferences("connection", MODE_PRIVATE);
        LinearLayout root = new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL);
        panel = new LinearLayout(this); panel.setOrientation(LinearLayout.VERTICAL);
        int padding = (int)(12 * getResources().getDisplayMetrics().density); panel.setPadding(padding, padding, padding, padding);
        TextView title = new TextView(this); title.setText(R.string.connection_title); panel.addView(title);
        host = field("电脑 IP（例如 192.168.1.10）", saved.getString("host", ""));
        port = field("端口", saved.getString("port", "27190")); port.setInputType(InputType.TYPE_CLASS_NUMBER);
        token = field("64 位配对码", saved.getString("token", "")); token.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        connect = new Button(this); connect.setText(R.string.connect); panel.addView(connect);
        TextView help = new TextView(this); help.setText(R.string.help); panel.addView(help);
        display = new DisplayView(this);
        display.setOnLongClickListener(v -> { panel.setVisibility(panel.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE); return true; });
        connect.setOnClickListener(v -> {
            if (desired) { desired = false; stopConnection(); connect.setText(R.string.connect); return; }
            try {
                address = host.getText().toString().trim(); tcpPort = Integer.parseInt(port.getText().toString());
                pairing = token.getText().toString().trim().toUpperCase(java.util.Locale.ROOT);
                if (!address.matches("(?:[0-9]{1,3}\\.){3}[0-9]{1,3}") || tcpPort < 1024 || tcpPort > 65535 || !pairing.matches("[A-F0-9]{64}"))
                    throw new IllegalArgumentException("请填写 IPv4、有效端口和完整配对码");
                getSharedPreferences("connection", MODE_PRIVATE).edit().putString("host", address).putString("port", Integer.toString(tcpPort)).putString("token", pairing).apply();
                desired = true; connect.setText(R.string.disconnect); startConnection();
            } catch (IllegalArgumentException ex) { new AlertDialog.Builder(this).setMessage(ex.getMessage()).setPositiveButton("知道了", null).show(); }
        });
        root.addView(panel); root.addView(display, new LinearLayout.LayoutParams(-1, 0, 1)); setContentView(root);
    }
    private EditText field(String hint, String value) {
        EditText field = new EditText(this); field.setHint(hint); field.setSingleLine(true); field.setText(value); panel.addView(field); return field;
    }
    @Override protected void onResume() { super.onResume(); active = true; if (desired) startConnection(); }
    @Override protected void onPause() { active = false; stopConnection(); super.onPause(); }
    @Override protected void onDestroy() { desired = false; stopConnection(); super.onDestroy(); }

    private void stopConnection() {
        Socket old;
        synchronized (this) { generation++; old = socket; socket = null; }
        if (old != null) try { old.close(); } catch (IOException ignored) { }
        display.disconnected("已断开 · 跨屏关闭");
    }
    private void startConnection() {
        stopConnection();
        final int attempt = generation;
        final String ip = address, key = pairing; final int serverPort = tcpPort;
        new Thread(() -> receive(attempt, ip, serverPort, key), "tablet-receiver").start();
    }
    private boolean current(int attempt) { return desired && active && attempt == generation; }
    private void receive(int attempt, String ip, int serverPort, String key) {
        while (current(attempt)) {
            try (Socket local = new Socket()) {
                // Publish before connect so lifecycle cancellation can interrupt it.
                synchronized (this) { if (!current(attempt)) return; socket = local; }
                local.connect(new InetSocketAddress(ip, serverPort), 3000); local.setSoTimeout(2500); local.setTcpNoDelay(true);
                DataInputStream input = new DataInputStream(local.getInputStream());
                DataOutputStream output = new DataOutputStream(local.getOutputStream());
                JSONObject hello = new JSONObject().put("version", 1).put("token", key);
                WirePacket.write(output, WirePacket.HELLO, hello.toString().getBytes(StandardCharsets.UTF_8));
                JSONObject status = new JSONObject();
                boolean presented = false;
                while (current(attempt)) {
                    WirePacket packet = WirePacket.read(input);
                    if (packet.type == WirePacket.ERROR) throw new IOException(new JSONObject(new String(packet.body, StandardCharsets.UTF_8)).optString("message"));
                    if (packet.type == WirePacket.STATUS) { status = new JSONObject(new String(packet.body, StandardCharsets.UTF_8)); continue; }
                    if (packet.type != WirePacket.FRAME) throw new IOException("收到意外数据");
                    long seq = ByteBuffer.wrap(packet.body, 0, 8).getLong();
                    Bitmap bitmap = BitmapFactory.decodeByteArray(packet.body, 8, packet.body.length - 8);
                    if (bitmap == null) throw new IOException("无法解码电脑画面");
                    boolean allowed = status.optBoolean("connected") && status.optBoolean("allowed") && !status.optBoolean("locked") && status.isNull("error");
                    boolean locked = status.optBoolean("locked"); String error = status.isNull("error") ? null : status.optString("error");
                    CountDownLatch rendered = new CountDownLatch(1);
                    boolean hidePanel = !presented; presented = true;
                    runOnUiThread(() -> {
                        if (current(attempt)) { if (hidePanel) panel.setVisibility(View.GONE); display.showFrame(bitmap, allowed, locked, error, rendered::countDown); }
                    });
                    if (!rendered.await(1200, TimeUnit.MILLISECONDS) || !current(attempt)) throw new IOException("平板画面显示暂停");
                    WirePacket.acknowledge(output, seq);
                }
            } catch (Exception ex) {
                String reason = ex.getMessage() == null ? "连接中断" : ex.getMessage();
                runOnUiThread(() -> { if (current(attempt)) display.disconnected(reason + " · 正在重连"); });
            } finally { synchronized (this) { if (attempt == generation) socket = null; } }
            try { Thread.sleep(1000); } catch (InterruptedException ex) { Thread.currentThread().interrupt(); return; }
        }
    }
}
