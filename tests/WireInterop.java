import org.tabletextend.receiver.WirePacket;
import java.io.*;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;

/** Real C# TCP -> Android's exact Java packet reader -> JPEG decoder, on JVM. */
public final class WireInterop {
    public static void main(String[] args) throws Exception {
        if (args[0].equals("fixture")) {
            BufferedImage image = new BufferedImage(3, 2, BufferedImage.TYPE_INT_RGB);
            image.setRGB(1, 1, 0x55FF55);
            if (!ImageIO.write(image, "jpeg", new File(args[1]))) throw new IOException("No JPEG encoder");
            return;
        }
        try (Socket socket = new Socket("127.0.0.1", Integer.parseInt(args[0]))) {
            socket.setSoTimeout(3000);
            DataInputStream input = new DataInputStream(socket.getInputStream());
            DataOutputStream output = new DataOutputStream(socket.getOutputStream());
            WirePacket.write(output, WirePacket.HELLO, ("{\"version\":1,\"token\":\"" + "A".repeat(64) + "\"}").getBytes(StandardCharsets.UTF_8));
            for (int n = 0; n < 2; n++) {
                WirePacket state = WirePacket.read(input);
                String json = new String(state.body, StandardCharsets.UTF_8);
                if (state.type != WirePacket.STATUS || !json.contains("\"allowed\":false") || !json.contains("\"connected\":" + (n > 0)))
                    throw new AssertionError("Wrong mouse state: " + json);
                WirePacket frame = WirePacket.read(input);
                if (frame.type != WirePacket.FRAME) throw new AssertionError("Missing frame");
                long sequence = ByteBuffer.wrap(frame.body, 0, 8).getLong();
                BufferedImage decoded = ImageIO.read(new ByteArrayInputStream(frame.body, 8, frame.body.length - 8));
                if (decoded == null || decoded.getWidth() != 3 || decoded.getHeight() != 2) throw new AssertionError("Wrong JPEG");
                WirePacket.acknowledge(output, sequence);
                System.out.println("PASS C# TCP -> Java receiver JPEG and status frame " + sequence);
            }
        }
    }
}
