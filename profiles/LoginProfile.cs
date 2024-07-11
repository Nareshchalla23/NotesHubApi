using AutoMapper;
using NotesHubApi.Models;
using System;

namespace NotesHubApi.Profiles
{
    public class LoginProfile : Profile
    {
        public LoginProfile()
        {
            CreateMap<LoginDto.Register, Jwtlogin>()
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username ?? string.Empty));

            CreateMap<LoginDto.Login, Jwtlogin>()
                .ForMember(dest => dest.LoginId, opt => opt.Ignore())
                .ForMember(dest => dest.Username, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedDate, opt => opt.Ignore());

            CreateMap<LoginDto.GoogleLogin, GoogleLogin>()
                .ForMember(dest => dest.LoginId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedDate, opt => opt.Ignore())
                .ForMember(dest => dest.LastLoginDate, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());

            CreateMap<GoogleLogin, LoginDto.GoogleLogin>();

            // New mapping for GitHubLogin
            CreateMap<LoginDto.GitHubLogin, GitHubLogin>()
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.GitHubId, opt => opt.MapFrom(src => src.Sub))
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Name)) // Assuming we use Name as Username
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.Picture))
                .ForMember(dest => dest.AccessToken, opt => opt.MapFrom(src => src.Code)) // Temporarily using Code as AccessToken
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastLoginDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.ModifiedDate, opt => opt.Ignore());

            CreateMap<GitHubLogin, LoginDto.GitHubLogin>()
                .ForMember(dest => dest.Sub, opt => opt.MapFrom(src => src.GitHubId))
                .ForMember(dest => dest.Picture, opt => opt.MapFrom(src => src.AvatarUrl))
                .ForMember(dest => dest.Code, opt => opt.MapFrom(src => src.AccessToken)); // Mapping AccessToken back to Code
        }
    }
}
