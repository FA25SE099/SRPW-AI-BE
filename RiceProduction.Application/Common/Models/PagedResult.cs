using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Models
{
    public class PagedResult<T> : Result<T>
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        public static PagedResult<T> Success(
            T data,
            int currentPage,
            int pageSize,
            int totalCount,
            string? message = null)
        {
            return new PagedResult<T>
            {
                Succeeded = true,
                Data = data,
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalCount = totalCount,
                Message = message
            };
        }

        public new static PagedResult<T> Failure(IEnumerable<string> errors, string? message = null)
        {
            return new PagedResult<T>
            {
                Succeeded = false,
                Message = message,
                Errors = errors
            };
        }

        public new static PagedResult<T> Failure(string error, string? message = null)
        {
            return new PagedResult<T>
            {
                Succeeded = false,
                Message = message,
                Errors = new[] { error }
            };
        }
    }
}
