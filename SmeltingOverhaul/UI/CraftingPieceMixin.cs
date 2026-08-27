using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using SmeltingOverhaul;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Library;

[ViewModelMixin] // 关键：必须标记 ViewModelMixin
public class CraftingPieceViewModelMixin : BaseViewModelMixin<CraftingPieceVM>
{
    private readonly CraftingPieceVM _vm;
    public CraftingPieceViewModelMixin(CraftingPieceVM vm) : base(vm)
    {
        _vm = vm;
        if (_vm.CraftingPiece == null || string.IsNullOrWhiteSpace(_vm.CraftingPiece.CraftingPiece.StringId))
        {
            return;
        }
        // 初始化数据（可从配置文件/游戏数据加载）
        var behavior = Campaign.Current?.GetCampaignBehavior<SmeltingMechanicBehavior>();
        PartCountText =  behavior.GetPartCount(_vm.CraftingPiece.CraftingPiece.StringId).ToString();
        SmeltingMechanicBehavior.OnDataUpdated += OnRefresh;
        // 通知 UI 属性变化（很重要！）
        OnPropertyChanged(nameof(PartCountText));
    }
    //public override void OnRefresh()无效
    //{
    //    var behavior = Campaign.Current?.GetCampaignBehavior<SmeltingMechanicBehavior>();
    //    if (behavior != null && _vm.CraftingPiece?.CraftingPiece?.StringId is string id)
    //    {
    //        PartCountText = behavior.GetPartCount(id).ToString();
    //        OnPropertyChanged(nameof(PartCountText));
    //    }
    //}

    [DataSourceProperty] // 关键：必须标记 DataSourceProperty
    public string PartCountText { get; set; }
}
[PrefabExtension("CraftingPieceGrid",
    "descendant::TextWidget[@Text='@TierText']")]
public class AddNewTextPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionText]
    public string GetReplacement() =>
        @"
        <TextWidget
            WidthSizePolicy=""CoverChildren""
            HeightSizePolicy=""CoverChildren""
            HorizontalAlignment=""Right""
            MarginTop=""25""
            MarginBottom=""25"" 
            MarginLeft=""25""
            MarginRight=""25""
            Brush=""Crafting.Card.Tier.Text""
            Text=""@PartCountText""/>
        ";
}

