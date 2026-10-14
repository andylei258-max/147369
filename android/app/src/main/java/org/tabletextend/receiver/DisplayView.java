package org.tabletextend.receiver;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.View;

/** Native Canvas, no WebView. Status is applied with its corresponding frame. */
public final class DisplayView extends View {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final RectF destination = new RectF();
    private Bitmap frame;
    private boolean allowed, locked;
    private String detail = "未连接 · 跨屏关闭";
    private Runnable rendered;
    public DisplayView(Context context) { super(context); setBackgroundColor(Color.BLACK); }
    public void showFrame(Bitmap bitmap, boolean crossing, boolean force, String error, Runnable onRendered) {
        frame = bitmap; allowed = crossing; locked = force;
        detail = error == null ? (allowed ? "允许鼠标跨屏" : "禁止鼠标跨屏") : error;
        rendered = onRendered; invalidate();
    }
    public void disconnected(String reason) {
        // Remove stale green state AND stale desktop on any network/lifecycle failure.
        frame = null; allowed = locked = false; detail = reason; rendered = null; invalidate();
    }
    @Override protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        if (frame != null) {
            float scale = Math.min((float)getWidth() / frame.getWidth(), (float)getHeight() / frame.getHeight());
            float w = frame.getWidth() * scale, h = frame.getHeight() * scale;
            destination.set((getWidth()-w)/2, (getHeight()-h)/2, (getWidth()+w)/2, (getHeight()+h)/2);
            canvas.drawBitmap(frame, null, destination, paint);
        }
        float density = getResources().getDisplayMetrics().density;
        canvas.save(); canvas.scale(density, density);
        paint.setColor(0xDD151515); canvas.drawRoundRect(8, 8, Math.min(getWidth()/density-8, 450), 54, 8, 8, paint);
        paint.setColor(allowed ? 0xFF2DCE72 : 0xFFFF5555);
        // Mouse shape with an unambiguous red/green indicator.
        canvas.drawRoundRect(18, 17, 36, 43, 7, 7, paint);
        paint.setColor(Color.BLACK); canvas.drawLine(27, 18, 27, 28, paint);
        paint.setColor(Color.WHITE); paint.setTextSize(16);
        canvas.drawText(detail, locked ? 80 : 48, 37, paint);
        if (locked) {
            paint.setStyle(Paint.Style.STROKE); paint.setStrokeWidth(2);
            canvas.drawRoundRect(54, 17, 66, 31, 6, 6, paint);
            paint.setStyle(Paint.Style.FILL); canvas.drawRoundRect(50, 27, 70, 43, 3, 3, paint);
        }
        canvas.restore();
        if (rendered != null) { Runnable callback = rendered; rendered = null; callback.run(); }
    }
}
