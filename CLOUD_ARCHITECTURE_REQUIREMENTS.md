# Cloud Architecture Requirements Document (Cost-Optimized)

## Executive Summary

Transform the Job Search Database Application into a **budget-conscious cloud demo** that showcases AI-assisted development capabilities while minimizing costs to **<$50/month**. This is a **portfolio project** designed to demonstrate technical skills to potential employers while keeping infrastructure costs minimal.

**Target Monthly Cost: $30-50/month** (vs. $241/month enterprise version)

---

## 1. Business Requirements

### 1.1 Primary Use Case

- **Portfolio/Demo project** to showcase AI-assisted full-stack development
- **Functional proof-of-concept** for cloud deployment
- **Cost-optimized** to run during unemployment
- **Single environment** (production only) to minimize costs

### 1.2 Key Stakeholders

- **Developer/Job Seeker**: Primary user, needs working demo
- **Recruiters/Hiring Managers**: View the demo to assess skills
- **Future Employers**: See live example of cloud architecture knowledge

### 1.3 Success Criteria

- ✅ Application runs reliably in the cloud
- ✅ Total monthly cost < $50
- ✅ Demonstrates modern cloud architecture patterns
- ✅ Professional appearance for portfolio
- ✅ Can be shown in job interviews

---

## 2. Functional Requirements (Simplified)

### 2.1 Portal & Application Selection

**Requirement**: Simple landing page (NOT full portal)

**Features**:

- Single-page landing with app description
- Direct link to job search application
- Professional branding
- Mobile-responsive

**Technical Approach**:

- Static S3 site with CloudFront CDN
- Simple HTML/CSS/JS
- CloudFront for global performance and professional appearance
- Cost: $10/month (CloudFront) + $1/month (S3)

### 2.2 User Management & Authentication

**Requirement**: Optional authentication (can start without)

**Options** (choose based on budget):

**Option A: No Auth (Cheapest - $0/month)**

- Public demo application
- No user accounts
- All data visible to everyone
- Good for: Quick demo, portfolio showcase

**Option B: Cognito (Free tier)**

- User registration/login
- First 50K MAU free
- Adds ~$0/month for demo usage
- Good for: Demonstrating auth skills

**✅ DECIDED: Option B - Cognito (Free Tier)**

- User registration/login required
- Demonstrates authentication skills
- Cost: $0/month (< 50K MAU)
- Groups: Users, Admins, ProgramManagers
- JWT validation in API

### 2.3 Job Search Application (Core)

**Requirement**: Existing functionality, cloud-hosted

**Features**:

- All core features (search, kanban, import)
- Single-tenant (no multi-tenancy complexity)
- Sample data pre-loaded
- No rate limiting needed

**Technical Approach**:

- Deploy existing React app to S3
- API runs on single ECS Fargate task
- Direct database access (no API Gateway)

### 2.4 Health Checks

**Requirement**: Single health check endpoint

**Tier 1: Simple Ping Only**

- Endpoint: `GET /health`
- Checks: API is running
- No authentication required
- Response: `200 OK` with `{ "status": "healthy" }`

**Skip Tiers 2 & 3** to save costs and complexity

### 2.5 Logging & Observability

**Requirement**: Basic logging (NOT enterprise-grade)

**Features**:

- Console logging to CloudWatch (free tier)
- No S3 archival initially
- No correlation IDs initially
- Simple error messages

**Add Later** (when budget allows):

- Structured logging
- S3 archival
- Error code tracking

### 2.6 CI/CD Pipeline

**Requirement**: Simplified CI/CD (NOT 3 environments)

**Environments**:

1. **Production Only** (single environment)
   - Manual deployment via Azure DevOps
   - No dev/staging environments
   - Direct deploy to production

**Pipeline Stages** (simplified):

```
1. Build
   - dotnet build
   - npm run build

2. Terraform Plan
   - terraform init
   - terraform plan -var="enable_multi_region=false"
   - Store plan artifact

3. Deploy Infrastructure
   - terraform apply -var="enable_multi_region=false"
   - Wait for resources

4. Deploy Application
   - Push Docker to ECR
   - Update ECS service
```

**Multi-Region Configuration**:

- Terraform variable: `enable_multi_region` (default: `false`)
- Pipeline variable: `EnableMultiRegion` (default: `false`)
- When `true`: Deploys to both us-east-1 and us-west-2
- When `false`: Single region (us-east-1) only
- Cost impact: +$50-100/month when enabled

**Cost Savings**: No dev/staging = ~$50/month savings

### 2.7 Infrastructure as Code (Terraform)

**Requirement**: Minimal Terraform (NOT full enterprise setup)

**Resources to Manage**:

- Single VPC (simplified, 2 AZs)
- Single ECS Fargate task (0.5 vCPU, 1 GB)
- Single RDS instance (db.t3.micro or db.t3.small)
- Single S3 bucket (frontend)
- Single ALB (no API Gateway)
- Single Cognito User Pool (optional)
- Basic security groups

**Terraform Structure** (simplified):

```
terraform/
├── main.tf              # All resources in one file
├── variables.tf
├── terraform.tfvars
└── backend.tf           # S3 for state
```

### 2.8 Domain & DNS

**Requirement**: Use free/low-cost options

**Options**:

**Option A: AWS Free Tier (Recommended)**

- Use `*.elasticbeanstalk.com` or ALB DNS name
- No domain purchase needed
- Cost: $0

**Option B: Cheap Domain**

- Purchase domain on Namecheap/GoDaddy (~$10/year)
- Use Route 53 (~$1/month)
- Total: ~$2/month

**Recommendation**: Start with Option A, add domain later

### 2.9 Resilience & Disaster Recovery

**Requirement**: Minimal DR (NOT multi-region)

**Strategy**:

- Single region (us-east-1 only)
- RDS automated backups (7-day retention)
- ECS task auto-restart on failure
- NO cross-region replication
- NO Route 53 health checks

**RTO**: Accept 15-30 minutes downtime if failure occurs
**RPO**: Accept 1 day data loss (daily backups)

**Cost Savings**: No multi-region = ~$50-100/month savings

### 2.10 Cost Optimization (CRITICAL)

**Requirement**: Absolute minimum cost

**Strategies**:

**Database**:

- **SELECTED: RDS PostgreSQL db.t3.micro** (cheapest option)
  - 2 vCPU, 1 GB RAM
  - Multi-AZ: NO (single AZ only)
  - Estimated cost: $15-20/month
  - Alternative: db.t3.small ($30-40/month) if micro is too slow

**Compute**:

- ECS Fargate: 0.5 vCPU, 1 GB RAM
- Use Fargate Spot: 70% discount
- Estimated cost: $5-10/month
- Scale to 0 when not in use (scheduler)

**Storage**:

- S3 Standard: $0.023/GB/month
- Estimated: $1-2/month (small frontend bundle)

**CDN**:

- **CloudFront CDN (INCLUDED)**
  - Global CDN for fast performance
  - Professional appearance
  - DDoS protection (AWS Shield Basic)
  - Cost: $10/month (first 10 TB)

**Estimated Monthly Cost (Minimum)**:

- RDS PostgreSQL (db.t3.micro, single AZ): $15
- ECS Fargate Spot (0.5 vCPU, 1 GB): $5
- S3 Storage: $2
- ALB: $5
- Route 53 (optional): $1
- CloudWatch (free tier): $0
- **Total: ~$23-28/month**

**Estimated Monthly Cost (With Domain)**:

- RDS PostgreSQL (db.t3.micro): $15
- ECS Fargate Spot: $5
- S3 Storage: $2
- ALB: $5
- Route 53 + Domain: $3
- **Total: ~$30/month**

---

## 3. Non-Functional Requirements (Relaxed)

### 3.1 Security

- Basic security groups (ALB → ECS → RDS)
- Data encrypted at rest (RDS, S3)
- TLS for ALB (free ACM certificate)
- NO WAF (save $5-10/month)
- NO Shield Advanced

### 3.2 Performance

- API response time: < 500ms (p95) - relaxed from 200ms
- Page load time: < 5s (acceptable for demo)
- Database query time: < 200ms
- NO CDN (use ALB directly)

### 3.3 Scalability

- Single ECS task (no auto-scaling)
- Manual scaling if needed
- RDS db.t3.micro (no auto-scaling)

### 3.4 Availability

- 95% uptime acceptable (not 99.9%)
- Single AZ deployment
- NO multi-region DR
- Daily automated backups

### 3.5 Monitoring

- Basic CloudWatch metrics (free tier)
- NO custom dashboards initially
- Email alerts on critical errors (SNS free tier)

### 3.6 Compliance

- GDPR: Not required for demo
- Audit logs: Basic CloudWatch only
- Access logs: ALB access logs (optional)

---

## 4. Technical Architecture (Simplified)

### 4.1 High-Level Architecture

```
┌─────────────────────────────────────────┐
│           Internet                       │
└─────────────────┬───────────────────────┘
                  │
          ┌───────▼────────┐
          │  ALB           │
          │  (Free TLS)    │
          └───────┬────────┘
                  │
          ┌───────▼────────┐
          │  ECS Fargate   │
          │  (Spot, 0.5vCPU)│
          │  API + Frontend │
          └───────┬────────┘
                  │
          ┌───────▼────────┐
          │  RDS PostgreSQL│
          │  (db.t3.micro) │
          │  (Single AZ)   │
          └────────────────┘

┌─────────────────────────────────────────┐
│  S3 Bucket (Frontend Static Files)      │
│  - Served via ALB or direct S3 URL      │
└─────────────────────────────────────────┘
```

**NO**:

- CloudFront CDN
- Route 53 (use ALB DNS)
- Cognito (initially)
- Multi-region
- API Gateway

### 4.2 Network Architecture (Minimal)

**VPC** (simplified):

- Public Subnet (1 AZ only):
  - ALB
  - NAT Gateway (optional, can use public IP for ECS)
- Private Subnet (1 AZ):
  - ECS Fargate task
  - RDS instance

**Security Groups**:

- ALB: Allow 80/443 from internet
- ECS: Allow 3000/5000 from ALB
- RDS: Allow 5432 from ECS only

### 4.3 Data Architecture (Single-Tenant)

**Strategy**: Single-tenant (simplest, cheapest)

**No multi-tenancy needed** - this is a demo app

**Database Schema**: Use existing schema (no changes needed initially)

**Backup Strategy**:

- RDS automated backups: 7-day retention
- Manual export to S3: Weekly
- NO cross-region replication

---

## 5. Migration Plan (Accelerated)

### Phase 1: Minimal Infrastructure (Week 1)

- [ ] Create AWS account (free tier)
- [ ] Set up Terraform backend (S3)
- [ ] Deploy minimal VPC + ALB
- [ ] Deploy RDS PostgreSQL (db.t3.micro)
- [ ] Deploy ECS Fargate (Spot)
- [ ] Deploy S3 bucket for frontend
- [ ] Configure ALB to serve frontend + proxy API

**Estimated Time**: 3-5 days  
**Estimated Cost**: ~$25/month

### Phase 2: Application Deployment (Week 2)

- [ ] Containerize API (Docker)
- [ ] Containerize frontend (Nginx)
- [ ] Push to ECR
- [ ] Deploy to ECS
- [ ] Migrate data from SQLite to RDS
- [ ] Test all functionality

**Estimated Time**: 3-5 days  
**Estimated Cost**: ~$25-30/month

### Phase 3: CI/CD (Week 3)

- [ ] Set up Azure DevOps repo
- [ ] Create simple pipeline (build + deploy)
- [ ] Automate Terraform deployment
- [ ] Test deployment process

**Estimated Time**: 2-3 days  
**Estimated Cost**: No change

### Phase 4: Polish & Demo Prep (Week 4)

- [ ] Add sample data
- [ ] Test all features
- [ ] Create demo script
- [ ] Document for portfolio
- [ ] Record demo video

**Estimated Time**: 3-5 days  
**Estimated Cost**: No change

**Total Timeline**: 3-4 weeks (vs. 16 weeks enterprise version)

---

## 6. Cost Estimates (Realistic)

### 6.1 Monthly Costs (Minimum Viable)

**Production Environment**:

- RDS PostgreSQL (db.t3.micro, single AZ): $15
- ECS Fargate Spot (0.5 vCPU, 1 GB): $5
- S3 Storage (5 GB): $1
- ALB: $5
- Data Transfer: $2
- CloudWatch (free tier): $0
- **Total: ~$28/month**

**With Domain**:

- RDS PostgreSQL: $15
- ECS Fargate Spot: $5
- S3 Storage: $1
- ALB: $5
- Route 53 + Domain: $3
- **Total: ~$30/month**

### 6.2 Cost Optimization Strategies

1. **Use Fargate Spot**: 70% discount on compute
   - Savings: $10-15/month

2. **Single AZ RDS**: No Multi-AZ
   - Savings: $15-20/month

3. **No CloudFront**: Use ALB directly
   - Savings: $10/month

4. **No Multi-Region**: Single region only
   - Savings: $50-100/month

5. **Shutdown nights/weekends** (optional):
   - Use EventBridge to stop ECS
   - Savings: $2-5/month

6. **Use Free Tier** (first 12 months):
   - 750 hours t2.micro/t3.micro RDS
   - 5 GB S3 storage
   - 1M ALB requests
   - Savings: $15-20/month (first year)

**Optimized Monthly Cost**: **$20-30/month** (with free tier: $5-10/month)

### 6.3 One-Time Costs

- Domain registration: $10-15/year (optional)
- SSL certificates: Free (ACM)
- **Total: $0-15 one-time**

---

## 7. Open Questions & Decisions Needed

### 7.1 Authentication (Critical for Cost)

**Question**: Start with or without authentication?

- **A) No Auth** (cheapest, simplest)
  - Cost: $0
  - Demo: Public, anyone can use
  - Risk: Data visible to all

- **B) Cognito** (free tier)
  - Cost: $0 (< 50K MAU)
  - Demo: Shows auth skills
  - Benefit: More realistic

**Recommendation**: Start with A, add B later if needed

### 7.2 Database Instance Size

**Question**: db.t3.micro or db.t3.small?

- **A) db.t3.micro** (2 vCPU, 1 GB)
  - Cost: $15/month
  - Risk: May be slow with sample data

- **B) db.t3.small** (2 vCPU, 2 GB)
  - Cost: $30/month
  - Benefit: Better performance

**Recommendation**: Start with A, upgrade to B if needed

### 7.3 Frontend Hosting

**Question**: S3 + ALB or separate hosting?

- **A) S3 + ALB** (integrated)
  - Cost: Included in ALB
  - Setup: More complex

- **B) S3 static website** (simpler)
  - Cost: $1-2/month
  - Setup: Easier

**Recommendation**: Option B for simplicity

### 7.4 Domain Name

**Question**: Purchase domain or use AWS URL?

- **A) No domain** (use ALB DNS)
  - Cost: $0
  - URL: `http://myapp.us-east-1.elb.amazonaws.com`

- **B) Purchase domain**
  - Cost: $2-3/month
  - URL: `https://myapp.com`

**Recommendation**: Start with A, add B for portfolio polish

---

## 8. Risks & Mitigations (Simplified)

### 8.1 Technical Risks

| Risk                      | Impact | Probability | Mitigation                           |
| ------------------------- | ------ | ----------- | ------------------------------------ |
| Fargate Spot interruption | Medium | Low         | Use on-demand for critical tasks     |
| RDS db.t3.micro too slow  | Medium | Medium      | Upgrade to db.t3.small ($15 more)    |
| Free tier expires         | High   | Certain     | Budget for $30/month after 12 months |
| Data loss                 | High   | Low         | Weekly manual backups to S3          |

### 8.2 Cost Risks

| Risk                      | Impact | Probability | Mitigation                         |
| ------------------------- | ------ | ----------- | ---------------------------------- |
| Unexpected charges        | High   | Low         | Set up billing alarms at $40/month |
| Free tier limits exceeded | Medium | Medium      | Monitor usage in AWS Console       |
| Forgetting to shut down   | Low    | Medium      | Use scheduler or manual stop       |

### 8.3 Operational Risks

| Risk                     | Impact | Probability | Mitigation                              |
| ------------------------ | ------ | ----------- | --------------------------------------- |
| Single point of failure  | High   | Medium      | Acceptable for demo, document in README |
| No staging environment   | Medium | Low         | Test locally before deploying           |
| Manual deployment errors | Medium | Medium      | Use Terraform (infrastructure as code)  |

---

## 9. Success Metrics (Adjusted for Demo)

### 9.1 Technical Metrics

- **Uptime**: 95% (acceptable for demo)
- **API Response Time**: < 500ms (p95)
- **Page Load Time**: < 5s
- **Error Rate**: < 1%
- **Deployment**: Manual, as needed

### 9.2 Business Metrics

- **Portfolio Views**: Track with Google Analytics (free)
- **Demo Completions**: Users can complete key flows
- **Resume Mentions**: Listed on resume/LinkedIn
- **Interview Conversations**: Generated from demo

### 9.3 Cost Metrics

- **Monthly Cost**: < $50 (target: $30)
- **Cost per Demo View**: < $0.10
- **ROI**: High (job opportunity value >> $30/month)

---

## 10. Implementation Checklist

### Pre-Launch (Week 1-2)

- [ ] Create AWS account
- [ ] Set up billing alerts ($40 threshold)
- [ ] Create Terraform backend
- [ ] Deploy infrastructure (VPC, ALB, RDS, ECS, S3)
- [ ] Migrate database from SQLite to RDS
- [ ] Deploy application
- [ ] Test all features
- [ ] Add sample data

### Launch (Week 3)

- [ ] Deploy to production
- [ ] Test from external network
- [ ] Document deployment process
- [ ] Create README for portfolio
- [ ] Record demo video (optional)

### Post-Launch (Ongoing)

- [ ] Monitor costs weekly
- [ ] Backup database weekly
- [ ] Update content as needed
- [ ] Add features incrementally
- [ ] Document learnings for interviews

---

## 11. Cost Comparison

### Enterprise Version (Original Plan)

- Monthly Cost: $241/month
- Timeline: 16 weeks
- Complexity: High
- Features: Full multi-tenant, auth, DR, monitoring

### Demo Version (This Plan)

- Monthly Cost: $30/month
- Timeline: 3-4 weeks
- Complexity: Low-Medium
- Features: Core functionality, cloud-hosted, portfolio-ready

**Cost Savings**: $211/month (87% reduction)  
**Time Savings**: 12 weeks (75% faster)

---

## 12. Next Steps

### Immediate (This Week)

1. **Decision**: Choose authentication approach (Section 7.1)
2. **Decision**: Choose database size (Section 7.2)
3. **Setup**: Create AWS account
4. **Setup**: Install Terraform locally
5. **Setup**: Set up Azure DevOps repo

### Week 1

1. Write Terraform code for minimal infrastructure
2. Deploy to AWS
3. Test connectivity

### Week 2

1. Containerize application
2. Deploy to ECS
3. Migrate data

### Week 3

1. Set up CI/CD
2. Test deployment pipeline
3. Polish UI

### Week 4

1. Add sample data
2. Record demo
3. Update portfolio
4. Share with network

---

## 13. Portfolio Presentation

### How to Present This Demo

**Elevator Pitch**:

> "I built a full-stack job search application using AI assistance. It's deployed on AWS using ECS Fargate, RDS PostgreSQL, and S3, with infrastructure managed by Terraform and CI/CD via Azure DevOps. The entire cloud infrastructure costs less than $30/month."

**Key Talking Points**:

1. **Full-Stack Development**: React + C# ASP.NET Core
2. **Cloud Architecture**: AWS (ECS, RDS, S3, ALB)
3. **Infrastructure as Code**: Terraform
4. **CI/CD**: Azure DevOps pipelines
5. **Cost Optimization**: Demonstrated by $30/month total cost
6. **AI-Assisted**: Built with Google AI Studio
7. **Production-Ready**: Real deployment, not just localhost

**Demo Flow** (5 minutes):

1. Show landing page
2. Search for jobs
3. Add job to Kanban
4. Update status
5. Show architecture diagram
6. Discuss cost optimization decisions

---

## Document Metadata

- **Version**: 2.0 (Cost-Optimized)
- **Date**: 2024-01-15
- **Author**: Architecture Team
- **Status**: Approved for Implementation
- **Target Cost**: $30/month
- **Target Timeline**: 3-4 weeks

---

## References

- [AWS Free Tier](https://aws.amazon.com/free/)
- [Terraform AWS Examples](https://github.com/terraform-aws-modules)
- [Azure DevOps Documentation](https://docs.microsoft.com/azure/devops/)
- [ECS Fargate Spot](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-fargate-task-defs.html)
