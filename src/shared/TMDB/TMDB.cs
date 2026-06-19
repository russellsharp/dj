using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared.TMDB.Models;

namespace shared.TMDB
{
    public interface ITMDB
    {
        Task<MovieDetailsResponse?> GetMovie(int id);
        Task<MovieQueryResponse> QueryMovies(string query, int page = 1);
        List<Genre> GetGenres();
    }

    public class TMDB : ITMDB
    {
        private IRepo _repo;

        public TMDB(IRepo repo)
        {
            _repo = repo;
        }

        public List<Genre> GetGenres()
        {
            return _repo.MovieGenres().Genres;
        }

        public async Task<MovieDetailsResponse?> GetMovie(int id)
        {
            var result = _repo.TryMovie(id, out MovieDetailsResponse? movie);
            Debug.Assert(result);
            return movie;
        }

        public async Task<MovieQueryResponse> QueryMovies(string query, int page = 1)
        {
            return await _repo.Query(query, page);
        }
    }
}