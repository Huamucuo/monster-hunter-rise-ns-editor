using System.Buffers.Binary;
using System.Text;
namespace FreshFoundation;

// Generated synthetic structures only; no save, catalog or game asset input.
public static class R11SyntheticSamples
{
    private static byte[] U(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
    private static void Number(SyntheticR2 b, uint id, int tag, uint value) => b.Field(id, tag, () => { b.Pad(4); b.Word(4); b.Pad(4); b.Bytes.AddRange(U(value)); });
    private static void Object(SyntheticR2 b, uint id, uint count, Action body) => b.Field(id, 17, () => b.Body(count, body));
    private static void Array(SyntheticR2 b, uint id, int tag, uint width, uint[] values) => b.Field(id, -1, () => b.Array(tag, width, (uint)values.Length, 0, () => { foreach (uint value in values) { b.Pad((int)width); b.Bytes.AddRange(width == 1 ? [(byte)value] : U(value)); } }));
    public static byte[] Slot(bool compressed = true, int emptyRecords = 8, string owner = "Seed", bool missingMirror = false, bool mismatchMirror = false, int incompatibleEmpty = -1, bool duplicateOwner = false)
    {
        var b = new SyntheticR2();
        b.Root(0xA100, 2, () =>
        {
            Object(b, 1043188015, 3, () =>
            {
                Object(b, 884539280, 1, () => Number(b, 2249897735, 7, 2000));
                Object(b, 2442806060, 1, () => Number(b, 2994776752, 8, 5000));
                Object(b, 2675617066, 1, () => b.Field(2694263966, -1, () => b.Array(17, 8, (uint)(7 + emptyRecords), 1, () =>
                {
                    for (int at = 0; at < 7 + emptyRecords; at++)
                    {
                        int record = at;
                        bool empty = at >= 7;
                        uint type = empty ? 0u : at == 0 ? 1u : at == 6 ? 3u : 2u;
                        b.Body(8, () =>
                        {
                            Number(b, 2037761500, 1, type);
                            Number(b, 1854292354, 8, empty ? 0u : (uint)(101 + record));
                            Array(b, 4129230907, 1, 4, empty ? [0, 0, 0] : [11u + (uint)record, 12u + (uint)record, 0]);
                            Array(b, 347745453, 1, 1, empty ? [0, 0] : [2, 5]);
                            Array(b, 3058239487, 8, 4, empty ? [0, 0] : [3, 7]);
                            Array(b, 557620028, 8, 4, empty ? [0, 0, 0, 0] : [8, 6, 4, 2]);
                            b.Field(0xAA01, 16, () => b.Primitive(12));
                            Object(b, 0xAA02, 2, () => { Number(b, 0xAA03, 8, (uint)(900 + record)); Array(b, 0xAA04, 1, 4, record == incompatibleEmpty ? [1, 2] : [1, 2, 3]); });
                        });
                    }
                })));
            });
            b.Field(0xCC00, 15, () => b.Text("Untouched 😀"));
        });
        void Owner() => Object(b, 3575327903, missingMirror ? 1u : 2u, () =>
        {
            b.Field(45809936, 15, () => b.Text(owner));
            if (!missingMirror)
                Object(b, 257730785, 1, () => b.Field(264986686, 15, () => b.Text(mismatchMirror ? "Other" : owner)));
        });
        b.Root(0xA200, 1, Owner);
        if (duplicateOwner)
            b.Root(0xA201, 1, Owner);
        b.Root(0xA300, 2, () => { b.Field(4154407670, 15, () => b.Text(owner)); Number(b, 0xCC01, 8, 0xDEADC0DE); });
        return b.File(compressed);
    }
    public static byte[] SystemFile(string owner = "Seed", int selectedSlot = 1, bool malformed = false)
    {
        var b = new SyntheticR2();
        b.Root(0xB100, 2, () =>
        {
            b.Field(28334441, -1, () => b.Array(17, 8, malformed ? 2u : 3u, 1, () => { for (int at = 1; at <= (malformed ? 2 : 3); at++) { int slot = at; b.Body(2, () => { b.Field(4154407670, 15, () => b.Text(slot == selectedSlot ? owner : "Other" + slot)); Number(b, 884539280, 7, 2000); }); } }));
            Number(b, 0xCC02, 8, 0xF1234567);
        });
        return b.File(false);
    }
}
