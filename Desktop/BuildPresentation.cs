using FreshFoundation;
namespace RiseWorkshopDesktop;

internal static class BuildPresentation
{
    private static string L(string en, string zh) => EquipmentDraftForm.L(en, zh);
    internal static string Relations(BuildPlanner planner, GameCatalogEntry decoration) => !planner.ShowCandidates ? L("parameters hidden", "参数已隐藏") : decoration.SkillRelations is null ? L("skills unknown", "技能未知") : string.Join(", ", decoration.SkillRelations.Select(r => $"{planner.Skill(r.SkillId)?.Name(UiLanguage.Current) ?? "ID"} ({r.SkillId}) +{r.Level}"));
    internal static string Piece(BuildPlanner planner, PlannedPiece piece, DomainEditor? domain = null)
    {
        string text;
        if (piece.Template is not null)
        {
            var binding = piece.Template;
            text = L("ORIGINAL OWNED TEMPLATE — experimental box copy", "原始已有模板 — 实验性装备箱副本") +
                $"\r\n{L("Source record", "来源记录")} {binding.RecordIndex} · {L("Type", "类型")} {binding.Type} · ID {binding.Id}" +
                "\r\n" + L("Positions are planning labels. Actual socket capacities are unknown. Copies are never automatically equipped.", "位置仅用于规划。实际槽容量未知。副本不会自动穿戴。");
            if (domain is not null)
            {
                try
                {
                    var template = domain.ReadEquipmentTemplate(binding.RecordIndex);
                    if (template.SourceDigest != binding.SourceDigest || template.StateDigest != binding.StateDigest || template.Type != binding.Type || template.Id != binding.Id)
                        throw new InvalidOperationException(L("Binding is stale; choose a current original template.", "绑定已过期；请选择当前原始模板。"));
                    if (template.Pairs is not null)
                        text += "\r\n" + L("Actual source skill pairs: ", "实际来源技能配对：") + string.Join("; ", template.Pairs.Select(p => $"ID {p.Id} / {L("level", "等级")} {p.Level}"));
                }
                catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ArgumentException)
                {
                    text += "\r\n" + L("Unresolved source: ", "来源未解决：") + UiLanguage.T(e.Message);
                }
            }
            else
                text += "\r\n" + L("Select a character and revalidate this template binding before staging.", "暂存前请选择角色并重新核验模板绑定。");
        }
        else if (piece.Position == BuildPosition.Talisman)
            text = piece.Talisman is null ? L("None: no virtual talisman.", "无：未设置虚拟护石。") : L("VIRTUAL TALISMAN — local planning only", "虚拟护石 — 仅本地规划") + "\r\n" + string.Join("\r\n", piece.Talisman.Pairs.Select((p, i) => L("Pair ", "配对 ") + (i + 1) + $": ID {p.Id} / {L("level", "等级")} {p.Level}" + (planner.Skill(p.Id) is GameCatalogEntry entry ? " · " + entry.Name(UiLanguage.Current) : ""))) + "\r\n" + L("Planned slot levels: ", "规划槽等级：") + string.Join(" / ", piece.Talisman.SlotLevels);
        else
            text = CatalogReferenceView.Describe(planner.Reference(piece), planner.ShowCandidates, id => planner.Skill(id)?.Name(UiLanguage.Current) ?? "ID");
        text += "\r\n\r\n" + L("Planned decorations (offline slots only)", "规划装饰珠（仅离线槽）");
        for (int i = 0; i < 3; i++)
        {
            uint id = piece.Decorations[i];
            text += $"\r\n{i + 1}: ID {id}";
            if (id == 0)
            {
                text += " · " + L("None", "无");
                continue;
            }
            if (planner.Decoration(id) is GameCatalogEntry e)
                text += " · " + e.Name(UiLanguage.Current) + " · " + Relations(planner, e);
            else
                text += " · " + L("hidden / unresolved", "已隐藏 / 未解决");
        }
        var s = planner.PreviewPiece(piece);
        string V(object? x) => x?.ToString() ?? L("Unknown", "未知");
        text += "\r\n\r\n" + L("THIS PIECE — including valid planned decorations", "本单件贡献 — 包含有效规划装饰珠") + $"\r\n{L("Defense / weapon attack / affinity", "防御 / 武器攻击 / 会心")}: {V(s.Defense)} / {V(s.Attack)} / {V(s.Affinity)}\r\n{L("Fire / Water / Thunder / Ice / Dragon", "火 / 水 / 雷 / 冰 / 龙")}: {V(s.Resistances?.Fire)} / {V(s.Resistances?.Water)} / {V(s.Resistances?.Thunder)} / {V(s.Resistances?.Ice)} / {V(s.Resistances?.Dragon)}";
        foreach (var skill in s.Skills)
            text += $"\r\n{planner.Skill(skill.Id)?.Name(UiLanguage.Current) ?? "ID"} · ID {skill.Id}: {skill.Total} / {L("maximum", "上限")} {V(skill.DeclaredMaximum)} · {L("effective / excess", "有效预览 / 超出")} {skill.Effective} / {skill.Excess}";
        if (!planner.ShowCandidates)
            text += "\r\n" + L("Enable unverified candidates to use catalog reference parameters.", "启用未验证候选后使用目录参考参数。");
        if (s.Partial)
            text += "\r\n" + L("PARTIAL / excluded conflicts:", "部分结果 / 已排除冲突：") + "\r\n" + string.Join("\r\n", s.Issues.Select(BuildIssueText.Localize));
        return text;
    }
}
