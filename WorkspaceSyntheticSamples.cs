using System.Buffers.Binary;
using System.Text;
namespace FreshFoundation;
// Explicitly synthetic fixture factory for focused model tests and opt-in UI smoke.
// It never reads a save or modifies an existing document.
public static class WorkspaceSyntheticSamples
{
    private static uint Id(string name)=>ObservedChecksum.Calculate(Encoding.UTF8.GetBytes(name));
    private static void Primitive(SyntheticR2 b,uint width,byte[] bytes,bool supplied=false){if(!supplied){b.Pad(4);b.Word(width);}b.Pad((int)width);b.Bytes.AddRange(bytes);}
    private static byte[] U(uint value){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,value);return b;}
    private static void Number(SyntheticR2 b,string name,int tag,uint value)=>b.Field(Id(name),tag,()=>Primitive(b,4,U(value)));
    private static void Object(SyntheticR2 b,string name,uint count,Action fields)=>b.Field(Id(name),17,()=>b.Body(count,fields));
    private static void Values(SyntheticR2 b,string name,int tag,uint width,uint[] values)=>b.Field(Id(name),-1,()=>b.Array(tag,width,(uint)values.Length,0,()=>{foreach(uint value in values)Primitive(b,width,width==1?[(byte)value]:U(value),true);}));
    public static byte[] Slot(int slotNumber=1,bool compressed=true,bool missingMasterRank=false,bool duplicateMoneyPath=false,int auxiliaryCount=4,bool transferFixture=false,int decorationCount=3)
    {
        var b=new SyntheticR2();
        b.Root(0x100,1,()=>Object(b,"_Data1",1,()=>Number(b,"Irrelevant",7,999)));
        void MainData()=>Object(b,"_Data1",4,()=>
        {
            Object(b,"_HandMoney",1,()=>Number(b,"_Value",7,2000));Object(b,"_VillagePointData",1,()=>Number(b,"_Point",8,5000));
            Object(b,"_ItemBox",1,()=>b.Field(Id("_InventoryList"),-1,()=>b.Array(17,8,3,1,()=>
            {
                foreach(var item in transferFixture?new[]{(id:0x04000031u,count:10u),(id:DomainEditor.EmptyItemId,count:0u),(id:0x04000031u,count:4u)}:new[]{(id:0x04000031u,count:9999u),(id:DomainEditor.EmptyItemId,count:0u),(id:0x04000032u,count:4u)})b.Body(2,()=>{Object(b,"_ItemCount",2,()=>{Number(b,"_Id",1,item.id);Number(b,"_Num",7,item.count);});b.Field(Id("UnknownItemBytes"),16,()=>b.Primitive(12));});
            })));
            Object(b,"_EquipBox",1,()=>b.Field(Id("_WeaponArmorInventoryList"),-1,()=>b.Array(17,8,5,1,()=>
            {
                for(uint kind=0;kind<5;kind++){uint type=kind==4?3:kind;uint[] skill=kind==4?[5,2]:[2,0];uint[] level=kind==4?[7,3]:[3,0];b.Body(6,()=>{Number(b,"_IdType",1,type);Number(b,"_IdVal",8,100+kind);Values(b,"_TalismanSkillIdList",1,1,skill);Values(b,"_TalismanSkillLvList",8,4,level);Values(b,"_DecoIdList",1,4,new uint[]{11,12,0}.Take(decorationCount).ToArray());Values(b,"_TalismanDecoSlotNumList",8,4,Enumerable.Range(1,auxiliaryCount).Select(i=>(uint)i).ToArray());});}
            })));
        });
        b.Root(0x200,1,MainData);if(duplicateMoneyPath)b.Root(0x201,1,()=>Object(b,"_Data1",1,()=>Object(b,"_HandMoney",1,()=>Number(b,"_Value",7,123))));
        b.Root(0x300,missingMasterRank?1u:2u,()=>{Number(b,"_HunterRank",7,120);if(!missingMasterRank)Number(b,"_MasterRank",7,uint.MaxValue);});
        b.Root(0x400,1,()=>Object(b,"MyData",2,()=>{b.Field(Id("HunterName"),15,()=>b.Text("Synthetic Hunter "+slotNumber+"\0"));Number(b,"_MasterRank",8,888);}));
        return b.File(compressed);
    }
    public static byte[] SystemFile()
    {
        var b=new SyntheticR2();b.Root(0x500,1,()=>b.Field(Id("_HunterArray"),-1,()=>b.Array(17,8,3,1,()=>{for(int i=1;i<=3;i++){int slot=i;b.Body(4,()=>{b.Field(Id("_HunterName"),15,()=>b.Text("Synthetic Hunter "+slot+"\0"));Number(b,"_HunterRank",7,120);Number(b,"_MasterRank",7,uint.MaxValue);Number(b,"_HandMoney",7,2000);});}})));return b.File(false);
    }
}
