using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tus_demo.Endpoints;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace tus_demo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(CreateTusConfiguration);

            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseTus(httpContext => Task.FromResult(httpContext.RequestServices.GetService<DefaultTusConfiguration>()));

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapGet("/files/{fileId}", DownloadFileEndpoint.HandleRoute);
            });
        }

        private DefaultTusConfiguration CreateTusConfiguration(IServiceProvider serviceProvider)
        {
            var env = (IWebHostEnvironment)serviceProvider.GetRequiredService(typeof(IWebHostEnvironment));

            //文件上传路径
            var tusFiles = Path.Combine(env.WebRootPath, "tusfiles");

            return new DefaultTusConfiguration
            {
                UrlPath = "/files",
                //文件存储路径
                Store = new TusDiskStore(tusFiles),
                //元数据是否允许空值
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
                //文件过期后不再更新
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5)),
                //事件处理（各种事件，满足你所需）
                Events = new Events
                {
                    //上传完成事件回调
                    OnFileCompleteAsync = async ctx =>
                    {
                        //获取上传文件
                        var file = await ctx.GetFileAsync();

                        //获取上传文件元数据
                        var metadatas = await file.GetMetadataAsync(ctx.CancellationToken);
                        
                        //获取上述文件元数据中的目标文件名称
                        var fileNameMetadata = metadatas["name"];

                        //目标文件名以base64编码，所以这里需要解码
                        var fileName = fileNameMetadata.GetString(Encoding.UTF8);

                        var extensionName = Path.GetExtension(fileName);

                        //将上传文件转换为实际目标文件
                        File.Move(Path.Combine(tusFiles, ctx.FileId), Path.Combine(tusFiles, $"{ctx.FileId}{extensionName}"));
                    }
                }
            };
        }
    }
}
