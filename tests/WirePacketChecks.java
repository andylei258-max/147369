import java.io.*;
import org.tabletextend.receiver.WirePacket;

public final class WirePacketChecks {
    private static int passed;
    private static void check(boolean ok, String name) { if (!ok) throw new AssertionError(name); passed++; System.out.println("PASS " + name); }
    public static void main(String[] args) throws Exception {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        WirePacket.acknowledge(new DataOutputStream(bytes), 0x0102030405060708L);
        byte[] ack = bytes.toByteArray();
        check(ack.length == 13 && ack[0] == 4 && ack[4] == 8 && ack[5] == 1 && ack[12] == 8, "ACK matches C# big endian fixture");
        WirePacket packet = WirePacket.read(new DataInputStream(new ByteArrayInputStream(ack)));
        check(packet.type == WirePacket.ACK && packet.body.length == 8, "packet roundtrip");
        for (byte[] bad : new byte[][] {
            {3, 0x7f, (byte)0xff, (byte)0xff, (byte)0xff},
            {2, (byte)0xff, (byte)0xff, (byte)0xff, (byte)0xff},
            {3,0,0,0,8,0,0,0,0,0,0,0,0},
            {99,0,0,0,0},
            {2,0,0,0,2,42}
        }) {
            try { WirePacket.read(new DataInputStream(new ByteArrayInputStream(bad))); throw new AssertionError("accepted malformed packet"); }
            catch (IOException expected) { check(true, "malformed/truncated packet rejected"); }
        }
        System.out.println(passed + " Java wire checks passed. No Android renderer/device claim.");
    }
}
