using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace IGArchiver.Test;

public class ArchiverTest
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    [TestCase("kendythefairy_1736177739_3539637887764580061_53168312232.jpg", 
        "kendythefairy",
        1736177739,
        3539637887764580061,
        53168312232,
        true)]
    [TestCase("fianna_blanco__1736914657_3545819599606969038_8276869819.jpg", 
        "fianna_blanco_",
        1736914657,
        3545819599606969038,
        8276869819,
        true)]
    
    [TestCase("caca", 
        null,
        0,
        0,
        0,
        false)]
    
    [TestCase("caca_9", 
        null,
        0,
        0,
        0,
        false)]
    
    [TestCase("caca_9_8", 
        null,
        0,
        0,
        0,
        false)]
    
    [TestCase("caca_9_8_7.kk", 
        "caca",
        9,
        8,
        7,
        false)]
    
    [TestCase("caca_10_9_8_7.ll", 
        "caca_10",
        9,
        8,
        7,
        false)]
    
    [TestCase("caca_10_9000_8000_7000.kk", 
        "caca_10",
        9000,
        8000,
        7000,
        true)]
    
    [TestCase("_rpicado_453302921_1531554257440899_113268899145972804_n.dng", 
        "_rpicado",
        453302921,
        1531554257440899,
        113268899145972804,
        true)]
    
    [TestCase("_camila_st_1730938927_3495691544842104591_9157100311 (1).jpg", 
        "_camila_st",
        1730938927,
        3495691544842104591,
        9157100311,
        true)]
    
    [TestCase("_n.nayee__1625605018_2612086677546617565_1958213731.jpg", 
        "_n.nayee_",
        1625605018,
        2612086677546617565,
        1958213731,
        true)]
    
    [TestCase("Maria Jose Zamora Villalobos_1500601798_1588927167839905_1588927167839905.jpg", 
        "Maria Jose Zamora Villalobos",
        1500601798,
        1588927167839905,
        1588927167839905,
        true)]
    
    [TestCase("kia._lazz_1737906933_999999_54826691821.mp4", 
        "kia._lazz",
        1737906933,
        999999,
        54826691821,
        true)]
    // 
    //mylawlesscr_undefined_3382428032302596675_58381419710_card.jpg
    // nay_tkd10_1695608472_highlight18300633772051052
    //luci_lagrimitas_1729401244_3482792528954560822_6172894518_3201461876828312_5234939421322264034_n
    //alinabriancesco_362293921_1333953374195072_6334880466512458388_n
    public void IGFile_Parse_Parses_Correctly(
        string fileName, 
        string? expectedUserName, 
        long expectedPostId, 
        long expectedPicId, 
        long expectedUserId,
        bool isValid)
    {
        var result = IgFile.Parse(fileName);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.EqualTo(isValid));
            Assert.That(result.UserName, Is.EqualTo(expectedUserName));
            Assert.That(result.PostId, Is.EqualTo(expectedPostId));
            Assert.That(result.PictureId, Is.EqualTo(expectedPicId));
            Assert.That(result.UserId, Is.EqualTo(expectedUserId));
        });
    }
}