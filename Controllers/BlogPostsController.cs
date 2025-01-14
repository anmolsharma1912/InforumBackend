﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InforumBackend.Data;
using InforumBackend.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace InforumBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogPostsController : ControllerBase
    {
        private readonly InforumBackendContext _context;

        private readonly ILogger _logger;

        public BlogPostsController(InforumBackendContext context, ILoggerFactory logger)
        {
            _context = context;
            _logger = logger.CreateLogger("BlogPostsController");
        }

        // GET: api/BlogPosts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BlogPost>>> GetBlogPost([FromQuery] PageParameter pageParameter, string userId, Boolean starSort)
        {
            try
            {
                IOrderedQueryable<BlogPost> blogPosts;

                // Find Posts by UserId and sort by Star
                if (starSort && !String.IsNullOrEmpty(userId))
                {
                    blogPosts = _context.BlogPost.Where(bp => bp.AuthorId == userId).Include(bp => bp.Category).OrderByDescending(bp => bp.Star);
                }
                // Find Posts by UserId and sort by DatePosted
                else if (!String.IsNullOrEmpty(userId))
                {
                    blogPosts = _context.BlogPost.Where(bp => bp.AuthorId == userId).Include(bp => bp.Category).OrderByDescending(bp => bp.DatePosted);
                }
                // Get all Posts and sort by Star
                else if (starSort)
                {
                    blogPosts = _context.BlogPost.Include(bp => bp.Category).OrderByDescending(bp => bp.Star);
                }
                // Get all Posts and sort bt DatePosted
                else
                {
                    blogPosts = _context.BlogPost.Include(bp => bp.Category).OrderByDescending(bp => bp.DatePosted);
                }

                var paginationMetadata = new PaginationMetadata(blogPosts.Count(), pageParameter.PageNumber, pageParameter.PageSize);
                Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationMetadata));

                var posts = await blogPosts.Skip((pageParameter.PageNumber - 1) * pageParameter.PageSize).Take(pageParameter.PageSize).ToListAsync();

                return Ok(new
                {
                    posts = posts,
                    pagination = paginationMetadata
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        // GET: api/BlogPosts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BlogPost>> GetBlogPost(long id)
        {
            try
            {
                var blogPost = await _context.BlogPost.Include(bp => bp.Category).FirstOrDefaultAsync(i => i.Id == id);

                if (blogPost == null)
                {
                    _logger.LogInformation("BlogPost of id: {0} not found.", id);
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found"
                    });
                }

                return Ok(blogPost);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        // GET: api/BlogPosts/slug/5
        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<BlogPost>> GetBlogPostBySlug(string slug)
        {
            try
            {
                var blogPost = await _context.BlogPost.Include(bp => bp.Category).FirstOrDefaultAsync(i => i.Slug == slug);

                if (blogPost == null)
                {
                    _logger.LogInformation("BlogPost of slug: {0} not found.", slug);
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found"
                    });
                }

                return Ok(blogPost);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        // PUT: api/BlogPosts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Editor, Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBlogPost(long id, BlogPost blogPost)
        {
            if (id != blogPost.Id)
            {
                _logger.LogInformation("BlogPost of id: {0} not found.", id);
                return BadRequest(new
                {
                    Status = StatusCodes.Status400BadRequest,
                    Message = "Check if the Post data is Valid or not."
                });
            }

            try
            {
                // generate slug based on the PUT data
                blogPost.Slug = generateSlug(blogPost.Title, id);

                _context.Entry(blogPost).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                _logger.LogInformation("BlogPost of id: {0} updated.", id);

                return Ok(new
                {
                    Status = StatusCodes.Status200OK,
                    Message = "Post Updated Successfully."
                });
            }
            catch (System.Exception ex)
            {
                if (!BlogPostExists(id))
                {
                    _logger.LogInformation("BlogPost of id: {0} not found.", id);
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post Does not Exist"
                    });
                }
                else
                {
                    _logger.LogError(ex.ToString());
                    return BadRequest();
                }
            }
        }

        // POST: api/BlogPosts
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Editor, Admin")]
        [HttpPost]
        public async Task<ActionResult<BlogPost>> PostBlogPost(BlogPost blogPost)
        {
            try
            {
                // add a new BlogPost object
                _context.BlogPost.Add(blogPost);
                await _context.SaveChangesAsync(); // save the object

                // Get Title and Id from the saved BlogPost object and generate slug
                var slug = generateSlug(blogPost.Title, blogPost.Id);

                // assign generated slug to the BlogPost object
                blogPost.Slug = slug;

                // update and save the object
                _context.Update(blogPost);

                await _context.SaveChangesAsync();
                _logger.LogInformation("BlogPost created.");

                return StatusCode(StatusCodes.Status201Created, new
                {
                    Status = StatusCodes.Status201Created,
                    Message = "Post created Sucessfully."
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        // DELETE: api/BlogPosts/5
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlogPost(long id)
        {
            try
            {
                var blogPost = await _context.BlogPost.FindAsync(id);

                if (blogPost == null)
                {
                    _logger.LogInformation("BlogPost of id: {0} not found.", id);
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found"

                    });
                }

                // Find Comments for the BlogPost
                var comments = await _context.Comment.Where(c => c.PostId == id).ToListAsync();
                // find SubComments of all the Comments
                var subComments = await _context.SubComment.Where(sc => comments.Select(c => c.Id).Contains(sc.CommentId)).ToListAsync();
                // Find all Stars for the BlogPost
                var stars = await _context.Star.Where(s => s.BlogPostId == id).ToListAsync();

                // Delete all the SubComments
                _context.SubComment.RemoveRange(subComments);
                _logger.LogInformation("SubComments of BlogPost of id: {0} deleted.", id);
                // Delete all the Comments
                _context.Comment.RemoveRange(comments);
                _logger.LogInformation("Comments of BlogPost of id: {0} deleted.", id);
                // Delete all the Stars
                _context.Star.RemoveRange(stars);
                _logger.LogInformation("Stars of BlogPost of id: {0} deleted.", id);

                // De;ete the BlogPost
                _context.BlogPost.Remove(blogPost);
                await _context.SaveChangesAsync();
                _logger.LogInformation("BlogPost of id: {0} deleted.", id);

                return Ok(new
                {
                    Status = StatusCodes.Status200OK,
                    Message = "Post deleted successfully"
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        // POST: api/BlogPosts/star
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize]
        [HttpPost("star")]
        public async Task<IActionResult> StarBlogPost(Star starModel)
        {
            try
            {
                // Find if the Blog Post Exist or not
                var blogPost = await _context.BlogPost.FindAsync(starModel.BlogPostId);

                // Return 404 if the Blog Post does not exist
                // Else Continue
                if (blogPost == null)
                {
                    _logger.LogInformation("BlogPost of id: {0} not found.", starModel.BlogPostId);
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found."
                    });
                }

                // Check if the Star Entry Exist or not
                var starExist = _context.Star.Any(s => s.BlogPostId == starModel.BlogPostId && s.UserId == starModel.UserId);

                // If Star Entry Exist remove the Star Entry
                if (starExist)
                {
                    // Find and remove the Star Entry
                    var removeStar = _context.Star.Remove(_context.Star.FirstOrDefault(s => s.BlogPostId == starModel.BlogPostId && s.UserId == starModel.UserId));

                    // -1 the Star count if Star Entry Successfully Removed
                    if (removeStar != null)
                    {
                        _logger.LogInformation("Star Entry of BlogPostId: {0} and UserId: {1} removed.", starModel.BlogPostId, starModel.UserId);

                        blogPost.Star--;

                        _logger.LogInformation("Star Count of BlogPostId: {0} Decremented Successfully.", starModel.BlogPostId);

                        // Save Changes to the Database
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Database State Saved");
                    }

                    // Retutrn OK
                    return Ok(new
                    {
                        Status = StatusCodes.Status200OK,
                        Message = "Star removed Successfully."
                    });
                }
                // Else Add the Star Entry
                else
                {
                    // Add the Star Entry
                    var addStar = _context.Star.Add(starModel);

                    // +1 the count if Star Entry Successfully Added
                    if (addStar != null)
                    {
                        _logger.LogInformation("Star Entry of BlogPostId: {0} and UserId: {1} added.", starModel.BlogPostId, starModel.UserId);

                        blogPost.Star++;

                        _logger.LogInformation("Star Count of BlogPostId: {0} incremented Successfully.", starModel.BlogPostId);

                        // Save Changes to the Database
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Database State Saved");
                    }
                    // Retutrn OK
                    return Ok(new
                    {
                        Status = StatusCodes.Status200OK,
                        Message = "Star added Successfully."
                    });
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        // POST: api/BlogPosts/star/status
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize]
        [HttpPost("star/status")]
        public async Task<IActionResult> StarStatusBlogPost(Star starModel)
        {
            try
            {
                // Find if the Blog Post Exist or not
                var blogPost = await _context.BlogPost.FindAsync(starModel.BlogPostId);

                // Return 404 if the Blog Post does not exist
                // Else Continue
                if (blogPost == null)
                {
                    _logger.LogInformation("BlogPost of id: {0} not found.", starModel.BlogPostId);
                    return NotFound(new
                    {
                        Status = StatusCodes.Status404NotFound,
                        Message = "Post not found."
                    });
                }

                // Check if the Star Entry Exist or not
                var starExist = _context.Star.Any(s => s.BlogPostId == starModel.BlogPostId && s.UserId == starModel.UserId);

                _logger.LogInformation("Star Entry of BlogPostId: {0} and UserId: {1} found.", starModel.BlogPostId, starModel.UserId);

                // Retutrn OK
                return Ok(new
                {
                    StarExist = starExist
                });

            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        private bool BlogPostExists(long id)
        {
            return _context.BlogPost.Any(e => e.Id == id);
        }

        /// <summary>
        /// Method to generate a slug from title and id from the BlogPost object
        /// removes all the special characters and spaces and replaces them with dashes(-)
        /// concatenates cleaned title and id and returns the slug in format of title-id
        /// </summary>
        /// <param name="title"></param>
        /// <param name="id"></param>
        /// <returns>slug(title-id)</returns>
        private string generateSlug(string title, long id)
        {
            var slug = title.ToLower();

            // remove all uneeded characters
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            // remove multiple spaces
            slug = Regex.Replace(slug, @"\s+", " ").Trim();
            // replace spaces with dashes(-)
            slug = Regex.Replace(slug, @"\s", "-");
            // concatenate slug and id
            slug = slug + "-" + id;

            _logger.LogInformation("Slug generated for title: {0} and id: {1}", title, id);

            return slug;
        }
    }
}
