using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using DB = Autodesk.Revit.DB;
using DBX = RhinoInside.Revit.External.DB;


namespace RhinoInside.Revit.GH.Components
{
  public class DocumentViews : ElementCollectorComponent
  {
    public override Guid ComponentGuid => new Guid("DF691659-B75B-4455-AF5F-8A5DE485FA05");
    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override string IconTag => "V";
    protected override DB.ElementFilter ElementFilter => new DB.ElementClassFilter(typeof(DB.View));

    public DocumentViews() : base
    (
      name: "Views",
      nickname: "Views",
      description: "Get all document views",
      category: "Revit",
      subCategory: "Query"
    )
    { }

    protected override ParamDefinition[] Inputs => inputs;
    static readonly ParamDefinition[] inputs =
    {
      ParamDefinition.FromParam(DocumentComponent.CreateDocumentParam(), ParamVisibility.Voluntary),
      ParamDefinition.Create<Parameters.Param_Enum<Types.ViewDiscipline>>("Discipline", "D", "View discipline", GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Parameters.Param_Enum<Types.ViewType>>("Type", "T", "View type", DB.ViewType.Undefined, GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Param_String>("Name", "N", "View name", GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Parameters.View>("Template", "T", "Views template", GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Param_Boolean>("Is Template", "T", "View is template", false, GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Param_Boolean>("Is Assembly", "A", "View is assembly", false, GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Param_Boolean>("Is Printable", "P", "View is printable", true, GH_ParamAccess.item, optional: true),
      ParamDefinition.Create<Parameters.ElementFilter>("Filter", "F", "Filter", GH_ParamAccess.item, optional: true),
    };

    protected override ParamDefinition[] Outputs => outputs;
    static readonly ParamDefinition[] outputs =
    {
      ParamDefinition.Create<Parameters.View>("Views", "V", "Views list", GH_ParamAccess.list)
    };

    protected override void TrySolveInstance(IGH_DataAccess DA, DB.Document doc)
    {
      // grab input data =============================================================================================
      // we do not want to filter by parameter default values so in most cases we need to check whether the parameter
      // actually contains input data or is simply using the default value

      // check if filtering by Discipline is requested
      var viewDiscipline = default(DBX.ViewDiscipline);
      bool hasDisciplineFilter = DA.GetData("Discipline", ref viewDiscipline);

      // check if filtering by view system family is requested
      DB.ViewFamily viewSystemFamily = default;
      bool hasViewSystemFamily = DA.GetData("View System Family", ref viewSystemFamily);

      // get the strings to filter the results
      string name = null, titleOnSheet = null;
      // skips title if name is provided
      bool hasNameOrTitleFilter = DA.GetData("Name", ref name) || DA.GetData("Title On Sheet", ref titleOnSheet);

      // check if filtering by Template is requested
      var template = default(DB.View);
      bool hasTemplateFilter = DA.GetData("Template", ref template);

      // check if filtering by IsTemplate is requested
      bool IsTemplate = false;
      var _IsTemplate_ = Params.IndexOfInputParam("Is Template");
      bool nofilterIsTemplate = (!DA.GetData(_IsTemplate_, ref IsTemplate) && Params.Input[_IsTemplate_].DataType == GH_ParamData.@void);

      // check if filtering by IsAssembly is requested
      bool IsAssembly = false;
      var _IsAssembly_ = Params.IndexOfInputParam("Is Assembly");
      bool nofilterIsAssembly = (!DA.GetData(_IsAssembly_, ref IsAssembly) && Params.Input[_IsAssembly_].DataType == GH_ParamData.@void);

      // check if filtering by IsPrintable is requested
      bool IsPrintable = false;
      var _IsPrintable_ = Params.IndexOfInputParam("Is Printable");
      bool nofilterIsPrintable = (!DA.GetData(_IsPrintable_, ref IsPrintable) && Params.Input[_IsPrintable_].DataType == GH_ParamData.@void);

      // get custom filters if provided
      DB.ElementFilter filter = null;
      bool hasCustomFilter = DA.GetData("Filter", ref filter);

      // construct the filter and start filtering ====================================================================
      using (var collector = new DB.FilteredElementCollector(doc))
      {
        // grab all views
        var viewsCollector = collector.WherePasses(ElementFilter);

        // filter by custom filter
        if (hasCustomFilter)
          viewsCollector = viewsCollector.WherePasses(filter);

        // filter by discipline
        if (hasDisciplineFilter)
        {
          if (viewDiscipline == DBX.ViewDiscipline.NotSet)
          {
            string emptyValue = string.Empty;
            TryGetFilterStringParam(DB.BuiltInParameter.VIEW_DISCIPLINE, ref emptyValue, out var viewDisciplineFilter);
            viewsCollector = viewsCollector.WherePasses(viewDisciplineFilter);
          }
          else
          {
            TryGetFilterIntegerParam(DB.BuiltInParameter.VIEW_DISCIPLINE, (int) viewDiscipline, out var viewDisciplineFilter);
            viewsCollector = viewsCollector.WherePasses(viewDisciplineFilter);
          }
        }

        // filter by name or title
        if (hasNameOrTitleFilter)
        {
          // getting DB.BuiltInParameter.VIEW_NAME instead of view.Name for consistency
          if (TryGetFilterStringParam(DB.BuiltInParameter.VIEW_NAME, ref name, out var viewNameFilter))
            viewsCollector = viewsCollector.WherePasses(viewNameFilter);

          // DB.BuiltInParameter.VIEW_DESCRIPTION is "Title On Sheet" for views
          if (TryGetFilterStringParam(DB.BuiltInParameter.VIEW_DESCRIPTION, ref titleOnSheet, out var viewDescFilter))
            viewsCollector = viewsCollector.WherePasses(viewDescFilter);
        }

        // filter by template view
        if (hasTemplateFilter && TryGetFilterElementIdParam(DB.BuiltInParameter.VIEW_TEMPLATE, template?.Id ?? DB.ElementId.InvalidElementId, out var templateFilter))
          viewsCollector = viewsCollector.WherePasses(templateFilter);

        // the rest of checks need the actual view object
        var views = collector.Cast<DB.View>();

        // filter by view system family
        if (hasViewSystemFamily)
          views = views.Where(x => ((DB.ViewFamilyType) x.Document.GetElement(x.GetTypeId()))?.ViewFamily == viewSystemFamily);

        // filter by IsTemplate
        if (hasIsTemplateFilter)
          views = views.Where((x) => x.IsTemplate == IsTemplate);

        // filter by IsAssembly
        if (hasIsAssemblyFilter)
          views = views.Where((x) => x.IsAssemblyView == IsAssembly);

        // filter by IsPrintable
        if (hasIsPrintableFilter)
          views = views.Where((x) => x.CanBePrinted == IsPrintable);

        // remove anything that is a builtin type
        views = views.Where(x => x.ViewType != DB.ViewType.Internal
                              && x.ViewType != DB.ViewType.Undefined
                              // project and system browser windows are also DB.View
                              && x.ViewType != DB.ViewType.ProjectBrowser
                              && x.ViewType != DB.ViewType.SystemBrowser)
                     // skip all the schedules that are internal keynote schdules, or internal titleblock revision schedules
                     // titleblock revision schedules are defined inside the titleblock families. But revit creates internal rev schedules
                     // to mirror the titleblock revision schedules and to keep the revision data updated efficiently
                     .Where(x => x.ViewType == DB.ViewType.Schedule ? !((DB.ViewSchedule)x).IsInternalKeynoteSchedule && !((DB.ViewSchedule) x).IsTitleblockRevisionSchedule : true);

        DA.SetDataList("Views", views);
      }
    }
  }
}
