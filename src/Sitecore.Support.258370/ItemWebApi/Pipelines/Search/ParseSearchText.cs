using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Extensions.StringExtensions;
using Sitecore.ItemWebApi;
using Sitecore.ItemWebApi.Pipelines.Search;

namespace Sitecore.Support.ItemWebApi.Pipelines.Search
{
  public class ParseSearchText : Sitecore.ItemWebApi.Pipelines.Search.ParseSearchText
  {

    #region Fields

    /// <summary>
    /// The all item id field.
    /// </summary>
    public static readonly ID AllItemId = new ID("{56A04961-F8A0-45BC-A870-D7371FD09F47}");

    /// <summary>
    /// The search id field.
    /// </summary>
    public static readonly ID SearchId = new ID("{648CE334-864D-4373-A632-CD0DCA4E00B9}");

    /// <summary>
    /// The and splitter
    /// </summary>
    private static readonly string[] AndSplitter = new[]
    {
      " AND "
    };

    /// <summary>
    /// The or splitter
    /// </summary>
    private static readonly string[] OrSplitter = new[]
    {
      " OR "
    };

    #endregion

    #region Public methods

    /// <summary>Processes the specified args.</summary>
    /// <param name="args">The args.</param>
    public override void Process([NotNull] SearchArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      args.Queryable = this.Parse(args.ProviderSearchContext, args.SearchText);
    }

    #endregion

    #region Private methods

    /// <summary>Makes the wildcard.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Returns the string.</returns>
    [CanBeNull]
    private string MakeWildcard([NotNull] string text)
    {
      Debug.ArgumentNotNull(text, "text");

      while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
      {
        text = text.Replace("  ", " ");
      }

      return text.Trim().Replace(" ", "* ") + "*";
    }

    /// <summary>Makes the from to expression.</summary>
    /// <param name="name">The field name.</param>      
    /// <param name="value">The initial value frome parameters.</param>
    /// <returns>Returns the expression.</returns>
    private Expression<Func<ConvertedSearchResultItem, bool>> MakeFromToExpression(string name, string value)
    {
      var m = value.IndexOf(" TO ");
      var start = value.Mid(1, m - 1);
      var end = value.Mid(m + 4);
      end = end.Left(end.Length - 1);

      Expression<Func<ConvertedSearchResultItem, bool>> startExp = i => i[name].CompareTo(start) >= 0;
      Expression<Func<ConvertedSearchResultItem, bool>> endExp = i => i[name].CompareTo(end) <= 0;
      return startExp.And(endExp);
    }

    /// <summary>Parses the search text.</summary>
    /// <param name="providerSearchContext">The provider search context.</param>
    /// <param name="searchText">The search text.</param>
    /// <returns>The predicate.</returns>
    [NotNull]
    private IQueryable<ConvertedSearchResultItem> Parse([NotNull] IProviderSearchContext providerSearchContext, [NotNull] string searchText)
    {
      Debug.ArgumentNotNull(providerSearchContext, "providerSearchContext");
      Debug.ArgumentNotNull(searchText, "searchText");

      var queryableText = string.Empty;
      Expression<Func<ConvertedSearchResultItem, bool>> expression = null;

      // sample AND (calculateddimension:enormous) AND (_templatename:image)
      // (calculateddimension:enormous OR calculateddimension:mega large) AND (_templatename:image) AND (__smallupdateddate:[20110406 TO 20130406])
      var ands = searchText.Split(AndSplitter, StringSplitOptions.RemoveEmptyEntries);

      foreach (var and in ands)
      {
        var factor = and;

        if (factor.StartsWith("("))
        {
          factor = factor.Mid(1);
          factor = factor.Left(factor.Length - 1);

          Expression<Func<ConvertedSearchResultItem, bool>> r = null;
          var ors = factor.Split(OrSplitter, StringSplitOptions.RemoveEmptyEntries);

          foreach (var or in ors)
          {
            var n = or.IndexOf(':');
            var name = or.Left(n);
            var value = or.Mid(n + 1);

            // from to expression
            if (value.StartsWith("[") && value.Contains(" TO "))
            {
              var startExp = MakeFromToExpression(name, value);
              r = r == null ? startExp : r.Or(startExp);
            }
            else
            {
              Expression<Func<ConvertedSearchResultItem, bool>> e = i => i[name] == value;
              r = r == null ? e : r.Or(e);
            }
          }

          expression = expression == null ? r : expression.And(r);
        }
        else
        {
          queryableText += " " + factor;
        }
      }

      IQueryable<ConvertedSearchResultItem> queryable = this.GetQuery(providerSearchContext, queryableText);
      return expression == null ? queryable : queryable.Where(expression);
    }

    /// <summary>
    /// Gets the query.
    /// </summary>
    /// <param name="providerSearchContext">The provider search context.</param>
    /// <param name="queryText">The query text.</param>
    /// <returns>The <see cref="IQueryable"/>.</returns>
    private IQueryable<ConvertedSearchResultItem> GetQuery([NotNull] IProviderSearchContext providerSearchContext, [CanBeNull] string queryText)
    {
      QueryUtil queryUtil = ApiFactory.Instance.GetQueryUtil();
      if (string.IsNullOrEmpty(queryText))
      {
        return queryUtil.CreateQuery<ConvertedSearchResultItem>(providerSearchContext, string.Empty);
      }

      var text = queryText.Trim();
      var searchStringModel = new List<SearchStringModel>
      {
        new SearchStringModel()
        {
          Operation = "should",
          Type = "text",
          Value = this.MakeWildcard(text)
        }
      };

      return queryUtil.CreateQuery<ConvertedSearchResultItem>(providerSearchContext, searchStringModel);
    }

    #endregion

  }
}