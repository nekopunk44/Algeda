using Application.DTOs.RealtorActivity;
using AutoMapper;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Mappers
{
    public class RealtorActivityLogProfile : Profile
    {
        public RealtorActivityLogProfile()
        {
            CreateMap<RealtorActivityLog, ActivityLogResponse>();

            CreateMap<CreateActivityLogRequest, RealtorActivityLog>()
                .ConstructUsing(src =>
                    new RealtorActivityLog(
                        src.RealtorId,
                        src.Type,
                        src.Points));
        }
    }
}
