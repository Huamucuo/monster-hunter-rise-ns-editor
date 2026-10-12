using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
namespace FreshFoundation;

public sealed record StructureSummary(int Records, int ValueEvents, int MaximumDepth, int ConsumedCoordinate, int StringCount, int NonzeroPaddingBytes, int TrailingZeroBytes, IReadOnlyDictionary<int,int> TagCounts, IReadOnlyDictionary<string,int> WidthCounts, IReadOnlyDictionary<uint,int> ArrayModes);
// Coordinates refer to the original 12-byte prefix plus the structural payload,
// irrespective of compressed wrapper placement. Implied object events consume no tag.
public readonly record struct StructuralEvent(int Coordinate, int EndCoordinate, int Tag, int Depth, uint? Identifier, uint? ClassIdentifier, uint? Width, uint? Count, uint? Mode, bool ImpliedTag, int? DataCoordinate = null, int? ElementTag = null);
public sealed class StructuralReading
{
    private readonly byte[] payload;
    public string Status => "EXPERIMENTAL";
    public StructureSummary Summary {get;}
    public IReadOnlyList<StructuralEvent> Events {get;}
    internal StructuralReading(byte[] payload, StructureSummary summary, List<StructuralEvent> events)
    {this.payload=payload;Summary=summary;Events=Array.AsReadOnly(events.ToArray());}
    public byte[] CopyPayload() => (byte[])payload.Clone();
}
internal sealed class StructureWalker
{
    internal const int DepthLimit=128, EventLimit=2_000_000, FieldLimit=10_000, ArrayLimit=500_000, TextLimit=100_000;
    private readonly byte[] bytes;
    private int cursor, depthMaximum, nonzero, strings, roots;
    private readonly List<StructuralEvent> events=[];
    private readonly Dictionary<int,int> tags=[];
    private readonly Dictionary<string,int> widths=[];
    private readonly Dictionary<uint,int> modes=[];
    private static readonly UnicodeEncoding StrictUnicode=new(false,false,true);
    private StructureWalker(byte[] bytes) {this.bytes=bytes;}
    internal static StructuralReading Read(byte[] bytes) => new StructureWalker(bytes).Run();
    private int Coordinate => checked(cursor+12);
    private StructuralReading Run()
    {
        while(bytes.Length-cursor>3)
        {
            uint id=Word(); Object(0,id,true); roots++;
        }
        int trailing=bytes.Length-cursor;
        if(bytes.AsSpan(cursor).IndexOfAnyExcept((byte)0)>=0) throw Bad("Nonzero terminal remainder");
        cursor=bytes.Length;
        var summary=new StructureSummary(roots,events.Count,depthMaximum,Coordinate,strings,nonzero,trailing,new ReadOnlyDictionary<int,int>(tags),new ReadOnlyDictionary<string,int>(widths),new ReadOnlyDictionary<uint,int>(modes));
        return new(bytes,summary,events);
    }
    private int Begin(int tag,int depth)
    {
        if(depth>DepthLimit) throw Bad("Nesting policy exceeded");
        if(events.Count>=EventLimit) throw Bad("Value-event policy exceeded");
        depthMaximum=Math.Max(depthMaximum,depth);
        tags[tag]=tags.GetValueOrDefault(tag)+1;
        int index=events.Count;events.Add(default);return index;
    }
    private void Object(int depth,uint? id,bool implied)
    {
        int start=Coordinate,index=Begin(17,depth);
        uint count=Word(),classId=Word();
        if(count>FieldLimit)throw Bad("Object field policy exceeded");
        // Every field needs at least its identifier and tag, even before a value.
        if(count>(bytes.Length-cursor)/8)throw Bad("Truncated object field table");
        for(uint i=0;i<count;i++) {uint fieldId=Word();int tag=unchecked((int)Word());Value(tag,depth+1,fieldId,null,false);Align(4);}
        events[index]=new(start,Coordinate,17,depth,id,classId,null,count,null,implied);
    }
    private void Value(int tag,int depth,uint? id,uint? suppliedWidth,bool implied)
    {
        if(tag==17) {Object(depth,id,implied);return;}
        int start=Coordinate,index=Begin(tag,depth);
        uint? width=null,count=null,mode=null;
        int? dataCoordinate=null;
        int? arrayElementTag=null;
        switch(tag)
        {
            case -1:
                Align(4);
                int elementTag=unchecked((int)Word());uint elementWidth=Word();uint elements=Word();uint arrayMode=Word();
                arrayElementTag=elementTag;
                if(elements>ArrayLimit)throw Bad("Array element policy exceeded");
                if(arrayMode>1)throw Bad("Unknown array mode");
                modes[arrayMode]=modes.GetValueOrDefault(arrayMode)+1;
                // Prefix validation and specialized element treatment follow admitted F7.
                ValidateArrayPrefix(elementTag,elementWidth,elements,arrayMode);
                if(elements>EventLimit-events.Count)throw Bad("Value-event policy exceeded");
                for(uint i=0;i<elements;i++)
                {
                    if(arrayMode==1)Object(depth+1,null,true);
                    else Value(elementTag,depth+1,null,elementWidth,true);
                }
                Align(4);width=elementWidth;count=elements;mode=arrayMode;break;
            case 15:
                Align(4);uint units=Word();if(units>TextLimit)throw Bad("UTF-16 length policy exceeded");
                int length=checked((int)units*2);var raw=Take(length);
                try {StrictUnicode.GetCharCount(raw);}catch(DecoderFallbackException){throw Bad("Unrecognized UTF-16 sequence");}
                strings++;count=units;break;
            default:
                uint w;
                if(suppliedWidth is uint supplied)w=supplied;
                else {Align(4);w=Word();}
                ValidateTagWidth(tag,w);Align(w);dataCoordinate=Coordinate;Take(checked((int)w));width=w;
                var key=$"{tag}:{w}";widths[key]=widths.GetValueOrDefault(key)+1;break;
        }
        events[index]=new(start,Coordinate,tag,depth,id,null,width,count,mode,implied,dataCoordinate,arrayElementTag);
    }
    private static void ValidateArrayPrefix(int tag,uint width,uint count,uint mode)
    {
        bool admitted=mode==1 ? tag==17 && width==8 : (tag,width) switch
        {
            (2,1) or (7,4) or (16,12) or (1,4) or (8,4) or (16,16) or (15,32) or (11,4) or (9,8) or (1,1) or (16,8) or (16,116) or (1,2)=>true,
            (4,1)=>count==0,
            _=>false
        };
        if(!admitted)throw new InvalidDataException("UNSUPPORTED array prefix.");
    }
    private static void ValidateTagWidth(int tag,uint width)
    {
        bool admitted=tag switch {1=>width is 1 or 2 or 4,2 or 3=>width==1,7 or 8 or 11=>width==4,9 or 12=>width==8,16=>width is 4 or 8 or 12 or 16 or 116,17 or 15 or -1=>true,_=>false};
        if(!admitted)throw new InvalidDataException("UNSUPPORTED structural tag/width.");
    }
    private uint Word() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    private ReadOnlySpan<byte> Take(int count)
    {
        if(count<0 || count>bytes.Length-cursor)throw Bad("Truncated structural data");
        var span=bytes.AsSpan(cursor,count);cursor+=count;return span;
    }
    private void Align(uint width)
    {
        if(width==0)throw Bad("Zero alignment width");
        ulong position=(ulong)Coordinate,target=(position+width-1)&~((ulong)width-1);
        if(target<position || target>(ulong)bytes.Length+12)throw Bad("Alignment outside structural region");
        var padding=Take(checked((int)(target-position)));
        foreach(byte b in padding)if(b!=0)nonzero++;
    }
    private InvalidDataException Bad(string detail)=>new($"{detail} at structural coordinate {Coordinate}.");
}


