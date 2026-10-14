package org.tabletextend.receiver;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;

/** Wire protocol shared with C#: unsigned type, big-endian signed length/body. */
public final class WirePacket {
    public static final int HELLO = 1, STATUS = 2, FRAME = 3, ACK = 4, ERROR = 5;
    public static final int MAX_FRAME = 16 * 1024 * 1024 + 8;
    public final int type;
    public final byte[] body;
    public WirePacket(int type, byte[] body) { this.type = type; this.body = body; }
    public static WirePacket read(DataInputStream input) throws IOException {
        int type = input.readUnsignedByte();
        int length = input.readInt();
        int limit = type == FRAME ? MAX_FRAME : 4096;
        if (type < HELLO || type > ERROR || length < 0 || length > limit)
            throw new IOException("协议数据超出限制");
        byte[] body = new byte[length]; input.readFully(body);
        if (type == FRAME && length <= 8) throw new IOException("没有画面内容");
        return new WirePacket(type, body);
    }
    public static void write(DataOutputStream output, int type, byte[] body) throws IOException {
        output.writeByte(type); output.writeInt(body.length); output.write(body); output.flush();
    }
    public static void acknowledge(DataOutputStream output, long sequence) throws IOException {
        output.writeByte(ACK); output.writeInt(8); output.writeLong(sequence); output.flush();
    }
}
